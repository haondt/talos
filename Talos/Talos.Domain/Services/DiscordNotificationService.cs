using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Text;
using Talos.Core.Abstractions;
using Talos.Core.Models;
using Talos.Discord.Models;
using Talos.Domain.Abstractions;
using Talos.Domain.Models;
using Talos.ImageUpdate.ImageUpdating.Services;
using Talos.ImageUpdate.Redis.Services;
using Talos.ImageUpdate.Shared.Constants;
using Talos.ImageUpdate.UpdatePushing.Models;
using Talos.ImageUpdate.UpdatePushing.Services;

namespace Talos.Domain.Services
{
    public class DiscordNotificationService(
        ITracer<DiscordNotificationService> tracer,
        IOptions<DiscordSettings> discordSettings,
        DiscordSocketClient discordClient,
        IRedisProvider redisProvider,
        IImageUpdateDataRepository updateDataRepository,
        IPushQueueMutator pushQueue,
        DiscordClientState clientState) : IDiscordNotificationService
    {
        private readonly IDatabase _redis = redisProvider.GetDefaultDatabase();
        public const string CompleteInteractionPrefix = "complete-interaction";
        public const string ImageUpdateInteractionInfix = "image-update";

        private async Task<ISocketMessageChannel> GetChannelAsync()
        {
            await clientState.StartTask;
            using var _ = tracer.StartSpan(nameof(GetChannelAsync), SpanKind.Client);
            var channelId = discordSettings.Value.ChannelId;
            if (discordClient.GetChannel(channelId) is not ISocketMessageChannel channel)
                throw new InvalidOperationException($"Failed to retrieve channel {channelId}");
            return channel;
        }

        public async Task<string> CreateInteractionAsync(ScheduledPushWithIdentity push)
        {
            using var _ = tracer.StartSpan(nameof(CreateInteractionAsync), SpanKind.Client);

            var additionalDescriptionSb = new StringBuilder();
            additionalDescriptionSb.AppendLine("Would you like to");
            additionalDescriptionSb.AppendLine("- _push_ this update through to the host");
            additionalDescriptionSb.AppendLine("- _defer_ this reminder until the next time the update process runs");
            additionalDescriptionSb.AppendLine("- _ignore_ this reminder and skip the update");

            var embed = PrepareImageUpdateEmbed(push, additionalDescriptionSb.ToString())
                .Build();

            var buttonIdPrefix = $"{CompleteInteractionPrefix}-{ImageUpdateInteractionInfix}-";
            var pushButtonId = Guid.NewGuid().ToString();
            var deferButtonId = Guid.NewGuid().ToString();
            var ignoreButtonId = Guid.NewGuid().ToString();

            var pushButton = new ButtonBuilder()
                .WithCustomId(buttonIdPrefix + pushButtonId)
                .WithLabel("Push")
                .WithStyle(ButtonStyle.Success);
            var deferButton = new ButtonBuilder()
                .WithCustomId(buttonIdPrefix + deferButtonId)
                .WithLabel("Defer")
                .WithStyle(ButtonStyle.Primary);
            var ignoreButton = new ButtonBuilder()
                .WithCustomId(buttonIdPrefix + ignoreButtonId)
                .WithLabel("Ignore")
                .WithStyle(ButtonStyle.Danger);
            var component = new ComponentBuilder()
                .WithRows([
                    new ActionRowBuilder()
                        .WithButton(pushButton)
                        .WithButton(deferButton)
                        .WithButton(ignoreButton)])
                .Build();

            var channel = await GetChannelAsync();
            RestUserMessage message;
            using (tracer.StartSpan(nameof(ISocketMessageChannel.SendMessageAsync), SpanKind.Client))
                message = await channel.SendMessageAsync(embed: embed, components: component);
            var messageId = message.Id.ToString();
            var interactionData = new DiscordImageUpdateInteractionData
            {
                DeferButtonId = deferButtonId,
                PushButtonId = pushButtonId,
                IgnoreButtonId = ignoreButtonId,
                PendingPush = push,
            };

            using (tracer.StartSpan(nameof(IDatabase.StringSetAsync), SpanKind.Client))
            {
                await _redis.StringSetAsync(RedisNamespacer.Discord.Interaction.ImageUpdate(messageId), JsonConvert.SerializeObject(interactionData, SerializationConstants.SerializerSettings));
                await _redis.StringSetAsync(RedisNamespacer.Discord.Interaction.Component.Message(pushButtonId), messageId);
                await _redis.StringSetAsync(RedisNamespacer.Discord.Interaction.Component.Message(deferButtonId), messageId);
                await _redis.StringSetAsync(RedisNamespacer.Discord.Interaction.Component.Message(ignoreButtonId), messageId);
            }
            return messageId;
        }

        private static EmbedBuilder PrepareImageUpdateEmbed(ScheduledPushWithIdentity push, string? extraDescription = null)
        {
            var descriptionSb = new StringBuilder("**Image update available**\n");
            descriptionSb.AppendLine($"-# {new Uri(push.Identity.GitRemoteUrl).AbsolutePath.Trim('/')} » {push.Identity.ShortFriendlyHashData}");
            descriptionSb.AppendLine($"Current Version\n```\n{push.Push.CurrentVersionFriendlyString}\n```");
            descriptionSb.AppendLine($"New version\n```\n{push.Push.NewVersionFriendlyString}\n```");

            if (!string.IsNullOrEmpty(extraDescription))
                descriptionSb.AppendLine(extraDescription);

            return new EmbedBuilder()
                .WithDescription(descriptionSb.ToString())
                .WithColor(Color.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow);
        }

        public async Task DeleteInteraction(string id)
        {
            using var _ = tracer.StartSpan(nameof(DeleteInteraction), SpanKind.Client);

            var messageIdKey = RedisNamespacer.Discord.Interaction.ImageUpdate(id);
            var serialized = await _redis.StringGetAsync(messageIdKey);
            if (serialized.IsNullOrEmpty)
                return;
            var deserialized = JsonConvert.DeserializeObject<DiscordImageUpdateInteractionData>(serialized.ToString(), SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to deserialize {nameof(DiscordImageUpdateInteractionData)} for message {id}");

            await _redis.KeyDeleteAsync(RedisNamespacer.Discord.Interaction.Component.Message(deserialized.PushButtonId));
            await _redis.KeyDeleteAsync(RedisNamespacer.Discord.Interaction.Component.Message(deserialized.DeferButtonId));
            await _redis.KeyDeleteAsync(RedisNamespacer.Discord.Interaction.Component.Message(deserialized.IgnoreButtonId));
            await _redis.KeyDeleteAsync(messageIdKey);
        }

        public Task CompleteInteractionAsync(string id, SocketInteraction interaction)
        {
            if (id.StartsWith($"{ImageUpdateInteractionInfix}-"))
                return CompleteImageUpdateInteractionAsync(id[$"{ImageUpdateInteractionInfix}-".Length..],
                    (interaction as SocketMessageComponent) ?? throw new InvalidCastException($"Was expecting {nameof(SocketMessageComponent)} but received {interaction.GetType()}"));
            return Task.CompletedTask;
        }

        private async Task CompleteImageUpdateInteractionAsync(string buttonId, SocketMessageComponent interaction)
        {
            using var _ = tracer.StartSpan(nameof(CompleteImageUpdateInteractionAsync), SpanKind.Client);

            var componentKey = RedisNamespacer.Discord.Interaction.Component.Message(buttonId);
            var messageIdValue = await _redis.StringGetAsync(componentKey);
            if (messageIdValue.IsNull)
                throw new ArgumentException($"Unable to find db entry for buttonId {buttonId}");
            var messageId = messageIdValue.ToString();
            var messageIdKey = RedisNamespacer.Discord.Interaction.ImageUpdate(messageIdValue.ToString());

            var serialized = await _redis.StringGetAsync(messageIdKey);
            if (serialized.IsNullOrEmpty)
                return;
            var deserialized = JsonConvert.DeserializeObject<DiscordImageUpdateInteractionData>(serialized.ToString(), SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to deserialize {nameof(DiscordImageUpdateInteractionData)} for message {messageId}");

            ImageUpdateInteractionResolution resolution;

            if (buttonId == deserialized.PushButtonId)
            {
                await pushQueue.UpsertAndEnqueuePushAsync(deserialized.PendingPush);
                resolution = ImageUpdateInteractionResolution.Push;
            }
            else if (buttonId == deserialized.DeferButtonId)
            {
                await updateDataRepository.ClearImageUpdateDataCacheAsync(deserialized.PendingPush.Identity);
                resolution = ImageUpdateInteractionResolution.Defer;
            }
            else if (buttonId == deserialized.IgnoreButtonId)
            {
                var cached = await updateDataRepository.TryGetImageUpdateDataAsync(deserialized.PendingPush.Identity);
                if (!cached.IsSuccessful)
                    throw new InvalidOperationException($"Unable to find image update data with identity {deserialized.PendingPush.Identity}");

                if (!cached.Value.LastNotified.HasValue || deserialized.PendingPush.Push.IsNewerThan(cached.Value.LastNotified.Value))
                    cached.Value.LastNotified = new(deserialized.PendingPush.Push);

                if (cached.Value.Interaction.HasValue)
                {
                    if (cached.Value.Interaction.Value.InteractionId.TryGetValue(out var value) && value == messageId)
                        cached.Value.Interaction.Value.InteractionId = new();

                    if (!cached.Value.Interaction.HasValue || deserialized.PendingPush.Push.IsNewerThan(cached.Value.Interaction.Value.PendingPush))
                        cached.Value.Interaction.Value.PendingPush = deserialized.PendingPush.Push;
                }
                else
                {
                    cached.Value.Interaction = new(new()
                    {
                        PendingPush = deserialized.PendingPush.Push,
                    });
                }

                await updateDataRepository.SetImageUpdateDataAsync(deserialized.PendingPush.Identity, cached.Value);
                resolution = ImageUpdateInteractionResolution.Ignore;
            }
            else
            {
                throw new ArgumentException($"Could not find button id matching given button id {deserialized.IgnoreButtonId} in message {messageId}");
            }


            var pushButton = new ButtonBuilder()
                .WithLabel("Push")
                .WithCustomId(Guid.NewGuid().ToString())
                .WithStyle(resolution == ImageUpdateInteractionResolution.Push ? ButtonStyle.Primary : ButtonStyle.Secondary)
                .WithDisabled(true);
            var deferButton = new ButtonBuilder()
                .WithLabel("Defer")
                .WithCustomId(Guid.NewGuid().ToString())
                .WithStyle(resolution == ImageUpdateInteractionResolution.Defer ? ButtonStyle.Primary : ButtonStyle.Secondary)
                .WithDisabled(true);
            var ignoreButton = new ButtonBuilder()
                .WithLabel("Ignore")
                .WithCustomId(Guid.NewGuid().ToString())
                .WithStyle(resolution == ImageUpdateInteractionResolution.Ignore ? ButtonStyle.Primary : ButtonStyle.Secondary)
                .WithDisabled(true);
            var component = new ComponentBuilder()
                .WithRows([
                    new ActionRowBuilder()
                        .WithButton(pushButton)
                        .WithButton(deferButton)
                        .WithButton(ignoreButton)])
                .Build();

            await interaction.UpdateAsync(m => m.Components = component);

            await _redis.KeyDeleteAsync(messageIdKey);
            await _redis.KeyDeleteAsync(RedisNamespacer.Discord.Interaction.Component.Message(deserialized.PushButtonId));
            await _redis.KeyDeleteAsync(RedisNamespacer.Discord.Interaction.Component.Message(deserialized.DeferButtonId));
            await _redis.KeyDeleteAsync(RedisNamespacer.Discord.Interaction.Component.Message(deserialized.IgnoreButtonId));
        }

        public async Task Notify(ScheduledPushWithIdentity push)
        {
            var embed = PrepareImageUpdateEmbed(push)
                .Build();
            await (await GetChannelAsync()).SendMessageAsync(embed: embed);
        }

        public async Task Notify(PipelineCompletionEvent pipelineCompleted)
        {
            var descriptionSb = new StringBuilder("**Pipeline Completed**\n");
            descriptionSb.AppendLine($"-# {pipelineCompleted.RepositorySlug} » {pipelineCompleted.BranchSlug}\n");
            descriptionSb.AppendLine($"Pipeline [{pipelineCompleted.Id}]({pipelineCompleted.Url})");
            var statusSb = new StringBuilder($"{(pipelineCompleted.Status == PipelineStatus.Success ? "Succeeded" : "Failed")}");
            if (pipelineCompleted.Duration.HasValue)
                statusSb.Append($" in {pipelineCompleted.Duration.Value}");
            descriptionSb.AppendLine(statusSb.ToString());
            descriptionSb.AppendLine($"Commit [{pipelineCompleted.CommitShortSha}]({pipelineCompleted.CommitUrl})\n");
            if (pipelineCompleted.CommitTitle.HasValue)
                descriptionSb.AppendLine($"{pipelineCompleted.CommitTitle.Value}");
            if (pipelineCompleted.CommitMessage.HasValue)
                descriptionSb.AppendLine("> " + string.Join("\n> ", pipelineCompleted.CommitMessage.Value.Trim().Split('\n')));

            var embed = new EmbedBuilder()
                .WithDescription(descriptionSb.ToString())
                .WithColor(pipelineCompleted.Status == PipelineStatus.Success ? Color.Green : Color.Red)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            using var span = tracer.StartSpan(nameof(Notify), SpanKind.Client);
            try
            {
                await (await GetChannelAsync()).SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                span.SetStatusFailure(ex.Message);
                throw;
            }
        }
    }

    public enum ImageUpdateInteractionResolution
    {
        Push,
        Defer,
        Ignore
    }
}
