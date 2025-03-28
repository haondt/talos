using YamlDotNet.Serialization;

namespace Talos.Renovate.Models
{
    public class DockerComposeFile
    {
        public Dictionary<string, Service>? Services { get; set; }
    }

    public class Service
    {
        public string? Image { get; set; }

        [YamlMember(Alias = "x-talos", ApplyNamingConventions = false)]
        public TalosSettings? XTalos { get; set; }

        // compact form for configuration. some examples:
        // x-tl: x # skip
        // x-tl: ~ # max bump size patch, notify all
        // x-tl: +! # max bump size major, push all
        // x-tl: ^? # max bump size minor, prompt all
        // x-tl: ~:!? # max bump size patch, digest = push, patch = prompt
        // x-tl: @:!!!! # max bump size digest, digest = push, everything else is push but will be ignored because the max is digest
        // x-tl: +:! # max bump size major, digest = push, everything else is the default (notify)
        //
        // in sum:
        // x = skip
        // 1st character is the max bump size (+^~@) for (major, minor, patch, digest)
        // if there is no second character, notify all
        // if the second character is not a colon, then it is the strategy to use for all levels
        //  (*?.!) = notify, prompt, skip, push
        // if the second character is a colon, the following characters specify the strategy for the
        // digest, patch, minor and major in that order. if there are less than 4 characters given, 
        // then we assume prompt for the missing ones
        [YamlMember(Alias = "x-tl", ApplyNamingConventions = false)]
        public string? XTalosShort { get; set; }
    }

    public class TalosSettings
    {
        public bool Skip { get; set; } = false;
        public BumpSize Bump { get; set; } = BumpSize.Digest;
        public BumpStrategySettings Strategy { get; set; } = new();

        public static TalosSettings ParseShortForm(string shortForm)
        {
            shortForm = shortForm.Trim();

            if (shortForm == "x")
                return new() { Skip = true };
            var bump = shortForm[0] switch
            {
                '+' => BumpSize.Major,
                '^' => BumpSize.Minor,
                '~' => BumpSize.Patch,
                '@' => BumpSize.Digest,
                _ => throw new ArgumentException($"Unrecognized bump reference {shortForm[0]}")
            };
            if (shortForm.Length == 1)
                return new() { Skip = false, Bump = bump };
            if (shortForm[1] != ':')
            {
                var allStrategy = GetBumpStrategy(shortForm[1]);
                return new()
                {
                    Skip = false,
                    Bump = bump,
                    Strategy = new()
                    {
                        Digest = allStrategy,
                        Patch = allStrategy,
                        Minor = allStrategy,
                        Major = allStrategy
                    }
                };
            }

            var strategy = new BumpStrategySettings();
            var strategyString = shortForm[2..];
            if (strategyString.Length > 0)
                strategy.Digest = GetBumpStrategy(strategyString[0]);
            if (strategyString.Length > 1)
                strategy.Patch = GetBumpStrategy(strategyString[1]);
            if (strategyString.Length > 2)
                strategy.Minor = GetBumpStrategy(strategyString[2]);
            if (strategyString.Length > 3)
                strategy.Major = GetBumpStrategy(strategyString[3]);

            return new()
            {
                Skip = false,
                Bump = bump,
                Strategy = strategy
            };
        }

        private static BumpStrategy GetBumpStrategy(char shortForm)
        {
            return shortForm switch
            {
                '*' => BumpStrategy.Notify,
                '.' => BumpStrategy.Skip,
                '?' => BumpStrategy.Prompt,
                '!' => BumpStrategy.Push,
                _ => throw new ArgumentException($"Unrecognized bump strategy {shortForm}")
            };
        }
    }


    public class BumpStrategySettings
    {
        public BumpStrategy Digest { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Patch { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Minor { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Major { get; set; } = BumpStrategy.Notify;
    }

    public enum BumpStrategy
    {
        Notify,
        Prompt,
        Skip,
        Push,
    }
}
