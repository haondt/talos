using Haondt.Core.Models;

namespace Talos.ImageUpdate.Shared.Models
{
    public class TalosSettings
    {
        public bool Skip { get; set; } = false;
        public BumpSize Bump { get; set; } = BumpSize.Digest;
        public SyncSettings? Sync { get; set; }
        public BumpStrategySettings Strategy { get; set; } = new();

        public static DetailedResult<TalosSettings, string> ParseShortForm(string shortForm)
        {
            shortForm = shortForm.Trim();

            if (shortForm == "x")
                return new(new TalosSettings() { Skip = true });
            var bump = shortForm[0] switch
            {
                '+' => BumpSize.Major,
                '^' => BumpSize.Minor,
                '~' => BumpSize.Patch,
                '@' => BumpSize.Digest,
                _ => throw new ArgumentException($"Unrecognized bump reference {shortForm[0]}")
            };
            if (shortForm.Length == 1)
                return new(new TalosSettings() { Skip = false, Bump = bump });
            if (shortForm[1] != ':')
            {
                var allStrategy = GetBumpStrategy(shortForm[1]);
                if (!allStrategy.IsSuccessful)
                    return new(allStrategy.Reason);

                return new(new TalosSettings()
                {
                    Skip = false,
                    Bump = bump,
                    Strategy = new()
                    {
                        Digest = allStrategy.Value,
                        Patch = allStrategy.Value,
                        Minor = allStrategy.Value,
                        Major = allStrategy.Value
                    }
                });
            }

            var strategy = new BumpStrategySettings();
            var strategyString = shortForm[2..];
            if (strategyString.Length > 0)
            {
                var digestStrategy = GetBumpStrategy(strategyString[0]);
                if (!digestStrategy.IsSuccessful) return new(digestStrategy.Reason);
                strategy.Digest = digestStrategy.Value;
            }
            if (strategyString.Length > 1)
            {
                var patchStrategy = GetBumpStrategy(strategyString[1]);
                if (!patchStrategy.IsSuccessful) return new(patchStrategy.Reason);
                strategy.Patch = patchStrategy.Value;
            }
            if (strategyString.Length > 2)
            {
                var minorStrategy = GetBumpStrategy(strategyString[2]);
                if (!minorStrategy.IsSuccessful) return new(minorStrategy.Reason);
                strategy.Minor = minorStrategy.Value;
            }
            if (strategyString.Length > 3)
            {
                var majorStrategy = GetBumpStrategy(strategyString[3]);
                if (!majorStrategy.IsSuccessful) return new(majorStrategy.Reason);
                strategy.Major = majorStrategy.Value;
            }

            return new(new TalosSettings()
            {
                Skip = false,
                Bump = bump,
                Strategy = strategy
            });
        }

        private static DetailedResult<BumpStrategy, string> GetBumpStrategy(char shortForm)
        {
            return shortForm switch
            {
                '*' => new(BumpStrategy.Notify),
                '.' => new(BumpStrategy.Skip),
                '?' => new(BumpStrategy.Prompt),
                '!' => new(BumpStrategy.Push),
                _ => new($"Unrecognized bump strategy {shortForm}")
            };
        }
    }
}
