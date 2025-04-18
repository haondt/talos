using FluentAssertions;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Tests
{
    public class TalosSettingsTests
    {

        public static IEnumerable<object[]> GetXTalosShortFormTestData()
        {
            BumpStrategySettings allStrategy(BumpStrategy strategy) => new()
            {
                Digest = strategy,
                Patch = strategy,
                Minor = strategy,
                Major = strategy,
            };

            return new List<(string, TalosSettings)>
            {
                ("x", new TalosSettings() { Skip = true} ),
                ("+", new TalosSettings(){ Skip = false, Bump = BumpSize.Major, Strategy = allStrategy(BumpStrategy.Notify)} ),
                ("^", new TalosSettings(){ Skip = false, Bump = BumpSize.Minor, Strategy = allStrategy(BumpStrategy.Notify)} ),
                ("~", new TalosSettings(){ Skip = false, Bump = BumpSize.Patch, Strategy = allStrategy(BumpStrategy.Notify)} ),
                ("@", new TalosSettings(){ Skip = false, Bump = BumpSize.Digest, Strategy = allStrategy(BumpStrategy.Notify)} ),
                ("+!", new TalosSettings(){ Skip = false, Bump = BumpSize.Major, Strategy = allStrategy(BumpStrategy.Push)} ),
                ("^!", new TalosSettings(){ Skip = false, Bump = BumpSize.Minor, Strategy = allStrategy(BumpStrategy.Push)} ),
                ("~!", new TalosSettings(){ Skip = false, Bump = BumpSize.Patch, Strategy = allStrategy(BumpStrategy.Push)} ),
                ("@!", new TalosSettings(){ Skip = false, Bump = BumpSize.Digest, Strategy = allStrategy(BumpStrategy.Push)} ),
                ("+?", new TalosSettings(){ Skip = false, Bump = BumpSize.Major, Strategy = allStrategy(BumpStrategy.Prompt)} ),
                ("^*", new TalosSettings(){ Skip = false, Bump = BumpSize.Minor, Strategy = allStrategy(BumpStrategy.Notify)} ),
                ("~.", new TalosSettings(){ Skip = false, Bump = BumpSize.Patch, Strategy = allStrategy(BumpStrategy.Skip)} ),

                ("+:!!", new TalosSettings(){ Skip = false, Bump = BumpSize.Major, Strategy = new(){ Digest = BumpStrategy.Push, Patch = BumpStrategy.Push, Minor = BumpStrategy.Notify, Major= BumpStrategy.Notify} }),
                ("^:??", new TalosSettings(){ Skip = false, Bump = BumpSize.Minor, Strategy = new(){ Digest = BumpStrategy.Prompt, Patch = BumpStrategy.Prompt, Minor = BumpStrategy.Notify, Major= BumpStrategy.Notify} }),
                ("~:**", new TalosSettings(){ Skip = false, Bump = BumpSize.Patch, Strategy = new(){ Digest = BumpStrategy.Notify, Patch = BumpStrategy.Notify, Minor = BumpStrategy.Notify, Major= BumpStrategy.Notify} }),
                ("@:..", new TalosSettings(){ Skip = false, Bump = BumpSize.Digest, Strategy = new(){ Digest = BumpStrategy.Skip, Patch = BumpStrategy.Skip, Minor = BumpStrategy.Notify, Major= BumpStrategy.Notify} }),

                ("+:!?*.", new TalosSettings(){ Skip = false, Bump = BumpSize.Major, Strategy = new(){ Digest = BumpStrategy.Push, Patch = BumpStrategy.Prompt, Minor = BumpStrategy.Notify, Major= BumpStrategy.Skip} }),
                ("^:.*?!", new TalosSettings(){ Skip = false, Bump = BumpSize.Minor, Strategy = new(){ Digest = BumpStrategy.Skip, Patch = BumpStrategy.Notify, Minor = BumpStrategy.Prompt, Major= BumpStrategy.Push} }),
                ("~:**!!", new TalosSettings(){ Skip = false, Bump = BumpSize.Patch, Strategy = new(){ Digest = BumpStrategy.Notify, Patch = BumpStrategy.Notify, Minor = BumpStrategy.Push, Major= BumpStrategy.Push} }),
                ("@:..?.", new TalosSettings(){ Skip = false, Bump = BumpSize.Digest, Strategy = new(){ Digest = BumpStrategy.Skip, Patch = BumpStrategy.Skip, Minor = BumpStrategy.Prompt, Major= BumpStrategy.Skip} }),
            }.Select(q => new object[] { q.Item1, q.Item2 });
        }


        [Theory]
        [MemberData(nameof(GetXTalosShortFormTestData))]
        public void WillParseXTalosShortForm(string shortForm, TalosSettings expectedSettings)
        {
            var actualSettings = TalosSettings.ParseShortForm(shortForm);
            actualSettings.Value.Should().BeEquivalentTo(expectedSettings);

        }
    }
}
