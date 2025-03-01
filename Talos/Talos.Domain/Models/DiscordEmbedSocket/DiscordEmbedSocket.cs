using Discord;
using Talos.Domain.Abstractions;

namespace Talos.Domain.Models.DiscordEmbedSocket
{

    public class DiscordEmbedSocketOptions
    {
        public Haondt.Core.Models.Optional<string> Title { get; set; }
        public Haondt.Core.Models.Optional<Color> Color { get; set; }
        public Haondt.Core.Models.Optional<string> CancelButtonId { get; set; }

        public bool Ephemeral { get; set; } = false;
        public bool DeferOnCreate { get; set; } = false;
        public bool SendEmbedOnCreate { get; set; } = true;

    }

    public class DiscordEmbedSocket : IAsyncDisposable
    {

        private readonly IDiscordEmbedSocketConnector _connector;
        private readonly DiscordEmbedSocketOptions _options;
        private bool _isDisposed = false;
        public bool IsDisposed => _isDisposed;

        public DiscordEmbedState State { get; internal init; }

        private DiscordEmbedSocket(IDiscordEmbedSocketConnector connector, DiscordEmbedSocketOptions options)
        {
            _connector = connector;
            _options = options;
            State = new()
            {
                Title = _options.Title,
                CancelButtonId = _options.CancelButtonId,
                DefaultColor = options.Color,
                Emphemeral = options.Ephemeral
            };
        }

        public async Task RegenerateEmbed()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (!State.Dirty)
                return;

            await State.SendEmbedAsync(_connector);
            State.Dirty = false;
        }

        public void StageUpdate(Action<DiscordEmbedSocketUpdateBuilder> configureBuilder)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            var builder = new DiscordEmbedSocketUpdateBuilder();
            configureBuilder(builder);
            builder.Apply(this);
        }

        public Task UpdateAsync(Action<DiscordEmbedSocketUpdateBuilder> configureBuilder)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            StageUpdate(configureBuilder);
            return RegenerateEmbed();
        }

        public static async Task<DiscordEmbedSocket> OpenSocketAsync(
            IDiscordEmbedSocketConnector connector,
            Action<DiscordEmbedSocketOptions>? configureOptions = null)
        {
            var options = new DiscordEmbedSocketOptions();
            if (configureOptions != null)
                configureOptions(options);
            var socket = new DiscordEmbedSocket(connector, options);

            if (options.DeferOnCreate)
            {
                await connector.DeferAsync();
                socket.State.HasSentInitialResponse = true;
            }
            if (options.SendEmbedOnCreate)
            {
                await socket.UpdateAsync(b => b.SetDescription("### Talos is thinking..."));
                socket.StageUpdate(b => b.ClearDescription());
            }

            return socket;
        }

        public async ValueTask DisposeAsync()
        {
            if (!State.CancelButtonId.HasValue)
            {
                _isDisposed = true;
                return;
            }

            State.CancelButtonId = new();
            State.Dirty = true;
            await RegenerateEmbed();
            _isDisposed = true;
        }
    }


    public class DiscordEmbedSocketUpdateBuilder()
    {
        private readonly List<Action<DiscordEmbedSocket>> _actions = [];
        private bool _willDirtyState = false;

        public DiscordEmbedSocketUpdateBuilder ClearDescription()
        {
            _actions.Add(s =>
            {
                s.State.DescriptionParts = [];
            });
            _willDirtyState = true;
            return this;
        }

        public DiscordEmbedSocketUpdateBuilder SetDescription(string newDescription)
        {
            _actions.Add(s =>
            {
                s.State.DescriptionParts = [new() { Value = newDescription }];
            });
            _willDirtyState = true;
            return this;
        }
        public DiscordEmbedSocketUpdateBuilder SetDescriptionPart(Guid id, string newPart)
        {
            _actions.Add(s =>
            {
                var part = s.State.DescriptionParts.Single(p => p.Id == id);
                part.Value = newPart;
            });
            _willDirtyState = true;
            return this;
        }
        public DiscordEmbedSocketUpdateBuilder UpdateDescriptionPart(Guid id, Func<string, string> newPartFactory)
        {
            _actions.Add(s =>
            {
                var part = s.State.DescriptionParts.Single(p => p.Id == id);
                part.Value = newPartFactory(part.Value);
            });
            _willDirtyState = true;
            return this;
        }
        public DiscordEmbedSocketUpdateBuilder AddDescriptionPart(string newPart)
        {
            _actions.Add(s =>
            {
                s.State.DescriptionParts.Add(new() { Value = newPart });
            });
            _willDirtyState = true;
            return this;
        }
        public DiscordEmbedSocketUpdateBuilder AddDescriptionPart(string newPart, Guid id)
        {
            _actions.Add(s =>
            {
                s.State.DescriptionParts.Add(new() { Value = newPart, Id = id });
            });
            _willDirtyState = true;
            return this;
        }


        public DiscordEmbedSocketUpdateBuilder AddStaticField(DiscordEmbedField field)
        {
            _actions.Add(s =>
            {
                s.State.Fields.Add(field.Copy());
            });
            _willDirtyState = true;
            return this;
        }

        public void Apply(DiscordEmbedSocket socket)
        {
            foreach (var action in _actions)
                action(socket);
            if (_willDirtyState)
                socket.State.Dirty = true;
        }

        public DiscordEmbedSocketUpdateBuilder SetColor(Color red)
        {
            _actions.Add(s =>
            {
                s.State.DefaultColor = red;
            });
            _willDirtyState = true;
            return this;
        }

        public DiscordEmbedSocketUpdateBuilder SetTitle(string title)
        {
            _actions.Add(s =>
            {
                s.State.Title = title;
            });
            _willDirtyState = true;
            return this;
        }
    }

}
