using FluentAssertions;
using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;
using Talos.Renovate.Services;
using Talos.Renovate.Tests.Fakes;

namespace Talos.Renovate.Tests
{
    public class ImageUpdaterTests
    {
        private ImageUpdaterService GetSut(FakeSkopeoService skopeoService)
        {
            var mockRedisDatabase = new Mock<IDatabase>();
            var mockRedisProvider = new Mock<IRedisProvider>();
            mockRedisProvider.Setup(q => q.GetDatabase(It.IsAny<int>())).Returns(mockRedisDatabase.Object);
            var mockGitHostProvider = new Mock<IGitHostServiceProvider>();
            var mockDockerComposeFileService = new Mock<IDockerComposeFileService>();
            var mockGitServiceFactory = new Mock<IGitServiceFactory>();
            return new ImageUpdaterService(
                Options.Create(new Models.ImageUpdateSettings
                {
                }),
                Options.Create(new Models.ImageUpdaterSettings
                {
                }),
                new FakeLogger<ImageUpdaterService>(),
                new FakeNotificationService(),
                skopeoService,
                mockRedisProvider.Object,
                mockGitHostProvider.Object,
                mockDockerComposeFileService.Object,
                mockGitServiceFactory.Object);
        }

        public static IEnumerable<object[]> GetTestData()
        {
            var tagAndDigestsByName = new Dictionary<string, List<(string Tag, string Digest)>>
            {
                ["image0"] = [],
                ["image1"] = [
                    ("latest", "sha256:001"),
                    ("latest-rc-potato", "sha256:003"),
                ],
                ["image2"] = [
                    ("latest", "sha256:001"),
                    ("stable", "sha256:002"),
                    ("debian", "sha256:003"),
                ],
                ["image3"] = [
                    ("v2.3.4", "sha256:001"),
                    ("v2.3.3", "sha256:002"),
                    ("v2.3.5", "sha256:003"),
                    ("v2.4.4", "sha256:010"),
                    ("v2.4.3", "sha256:011"),
                    ("v2.4.5", "sha256:012"),
                    ("v2.3", "sha256:004"),
                    ("v2.4", "sha256:005"),
                    ("v2.2", "sha256:006"),
                    ("v2", "sha256:009"),
                    ("v1.2.3", "sha256:00a"),
                    ("v1.2", "sha256:00b"),
                    ("v1", "sha256:00c"),
                    ("v3.4.5", "sha256:00d"),
                    ("v3.4", "sha256:00e"),
                    ("v3", "sha256:00f"),
                    ("v4", "sha256:008"),
                ],
                ["image4"] = [
                    ("stable", "sha256:001")
                ],
                ["image5"] = [
                    ("latest-alpine", "sha256:001")
                ],
                ["image6"] = [
                    ("latest", "sha256:001"),
                    ("stable", "sha256:002"),
                    ("debian", "sha256:003"),
                    ("v2.3.4", "sha256:004"),
                    ("v2.3.3", "sha256:005"),
                    ("v2.3.5", "sha256:006"),
                    ("v2.3", "sha256:007"),
                    ("v2.4", "sha256:008"),
                    ("v2.2", "sha256:009"),
                    ("v3", "sha256:00a"),
                    ("v4", "sha256:00b"),
                    ("v2", "sha256:00c"),
                    ("latest-alpine", "sha256:00d"),
                    ("stable-alpine", "sha256:00e"),
                    ("debian-alpine", "sha256:00f"),
                    ("v2.3.4-alpine", "sha256:010"),
                    ("v2.3.3-alpine", "sha256:011"),
                    ("v2.3.5-alpine", "sha256:012"),
                    ("v2.3-alpine", "sha256:013"),
                    ("v2.4-alpine", "sha256:014"),
                    ("v2.2-alpine", "sha256:015"),
                    ("v3-alpine", "sha256:016"),
                    ("v4-alpine", "sha256:017"),
                    ("v2-alpine", "sha256:018"),
                    ("latest-debian", "sha256:019"),
                    ("stable-debian", "sha256:01a"),
                    ("debian-debian", "sha256:01b"),
                    ("v2.3.4-debian", "sha256:01c"),
                    ("v2.3.3-debian", "sha256:01d"),
                    ("v2.3.5-debian", "sha256:01e"),
                    ("v2.3-debian", "sha256:01f"),
                    ("v2.4-debian", "sha256:020"),
                    ("v2.2-debian", "sha256:021"),
                    ("v3-debian", "sha256:022"),
                    ("v4-debian", "sha256:023"),
                    ("v2-debian", "sha256:024"),
                ],
                ["image7"] = [
                    ("1", "sha256:001"),
                    ("1.1", "sha256:002"),
                    ("1.1.1", "sha256:003"),
                    ("1.1.2", "sha256:004"),
                    ("1.2", "sha256:005"),
                    ("1.2.1", "sha256:006"),
                    ("1.2.2", "sha256:007"),
                    ("2", "sha256:008"),
                    ("2.1", "sha256:009"),
                    ("2.1.1", "sha256:00a"),
                    ("2.1.2", "sha256:00b"),
                    ("2.2", "sha256:00c"),
                    ("2.2.2", "sha256:00d"),
                ],

            };

            var tagsByName = tagAndDigestsByName.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(q => q.Tag).ToList());
            var digestsByNameAndTag = tagAndDigestsByName.SelectMany(q => q.Value.Select(r => new
            {
                Name = q.Key,
                Tag = r.Tag,
                Digest = r.Digest
            })).ToDictionary(q => $"{q.Name}:{q.Tag}", q => q.Digest);

            return new List<Union<(string Name, Optional<string> CurrentTagAndDigest, BumpSize MaxBumpSize), (string Name, Optional<string> CurrentTagAndDigest, BumpSize MaxBumpSize, string ExpectedTagAndDigest, BumpSize BumpSize)>>
            {
                // test zero tags
                ("image0", new(), BumpSize.Major),

                // general testing
                ("image1", new(), BumpSize.Major, "latest@sha256:001", BumpSize.Digest),
                ("image1", "v1.2.3", BumpSize.Major),
                ("image1", "latest@sha256:001", BumpSize.Major),
                ("image1", "latest@sha256:aaa", BumpSize.Major, "latest@sha256:001", BumpSize.Digest),

                // test no tag
                ("image2", new(), BumpSize.Major, "latest@sha256:001", BumpSize.Digest),
                ("image3", new(), BumpSize.Major),
                ("image4", new(), BumpSize.Major),
                ("image5", new(), BumpSize.Major),
                ("image6", new(), BumpSize.Major, "latest@sha256:001", BumpSize.Digest),

                // test release version
                ("image2", "latest", BumpSize.Major, "latest@sha256:001", BumpSize.Digest),
                ("image2", "latest@sha256:000", BumpSize.Major, "latest@sha256:001", BumpSize.Digest),
                ("image2", "latest@sha256:001", BumpSize.Major),
                ("image2", "stable", BumpSize.Major, "stable@sha256:002", BumpSize.Digest),
                ("image2", "stable@sha256:000", BumpSize.Major, "stable@sha256:002", BumpSize.Digest),
                ("image2", "stable@sha256:002", BumpSize.Major),
                ("image3", "stable", BumpSize.Major),
                ("image4", "latest", BumpSize.Major),
                ("image5", "latest", BumpSize.Major),
                ("image6", "latest", BumpSize.Major, "latest@sha256:001", BumpSize.Digest),
                ("image6", "stable", BumpSize.Major, "stable@sha256:002", BumpSize.Digest),
                ("image6", "stable@sha256:000", BumpSize.Major, "stable@sha256:002", BumpSize.Digest),
                ("image6", "stable@sha256:002", BumpSize.Major),
                ("image6", "latest-alpine", BumpSize.Major, "latest-alpine@sha256:00d", BumpSize.Digest),
                ("image6", "stable-alpine", BumpSize.Major, "stable-alpine@sha256:00e", BumpSize.Digest),
                ("image6", "stable-alpine@sha256:000", BumpSize.Major, "stable-alpine@sha256:00e", BumpSize.Digest),
                ("image6", "stable-alpine@sha256:00e", BumpSize.Major),
                ("image6", "latest-ubuntu", BumpSize.Major),
                ("image6", "latest-ubuntu@sha256:000", BumpSize.Major),

                // test semantic version
                ("image3", "v2", BumpSize.Major, "v4@sha256:008", BumpSize.Major),
                ("image3", "v2", BumpSize.Minor, "v2@sha256:009", BumpSize.Digest),
                ("image3", "v2", BumpSize.Patch, "v2@sha256:009", BumpSize.Digest),
                ("image3", "v2", BumpSize.Digest, "v2@sha256:009", BumpSize.Digest),
                ("image3", "v2@sha256:000", BumpSize.Major, "v4@sha256:008", BumpSize.Major),
                ("image3", "v2@sha256:000", BumpSize.Minor, "v2@sha256:009", BumpSize.Digest),
                ("image3", "v2@sha256:000", BumpSize.Patch, "v2@sha256:009", BumpSize.Digest),
                ("image3", "v2@sha256:000", BumpSize.Digest, "v2@sha256:009", BumpSize.Digest),
                ("image3", "v2@sha256:009", BumpSize.Major, "v4@sha256:008", BumpSize.Major),
                ("image3", "v2@sha256:009", BumpSize.Minor),
                ("image3", "v2@sha256:009", BumpSize.Patch),
                ("image3", "v2@sha256:009", BumpSize.Digest),

                ("image3", "v2.3", BumpSize.Major, "v3.4@sha256:00e", BumpSize.Major),
                ("image3", "v2.3", BumpSize.Minor, "v2.4@sha256:005", BumpSize.Minor),
                ("image3", "v2.3", BumpSize.Patch, "v2.3@sha256:004", BumpSize.Digest),
                ("image3", "v2.3", BumpSize.Digest, "v2.3@sha256:004", BumpSize.Digest),
                ("image3", "v2.3@sha256:000", BumpSize.Major, "v3.4@sha256:00e", BumpSize.Major),
                ("image3", "v2.3@sha256:000", BumpSize.Minor, "v2.4@sha256:005", BumpSize.Minor),
                ("image3", "v2.3@sha256:000", BumpSize.Patch, "v2.3@sha256:004", BumpSize.Digest),
                ("image3", "v2.3@sha256:000", BumpSize.Digest, "v2.3@sha256:004", BumpSize.Digest),
                ("image3", "v2.3@sha256:004", BumpSize.Major, "v3.4@sha256:00e", BumpSize.Major),
                ("image3", "v2.3@sha256:004", BumpSize.Minor, "v2.4@sha256:005", BumpSize.Minor),
                ("image3", "v2.3@sha256:004", BumpSize.Patch),
                ("image3", "v2.3@sha256:004", BumpSize.Digest),

                ("image3", "v2.3.3", BumpSize.Major, "v3.4.5@sha256:00d", BumpSize.Major),
                ("image3", "v2.3.3", BumpSize.Minor, "v2.4.5@sha256:012", BumpSize.Minor),
                ("image3", "v2.3.3", BumpSize.Patch, "v2.3.5@sha256:003", BumpSize.Patch),
                ("image3", "v2.3.3", BumpSize.Digest, "v2.3.3@sha256:002", BumpSize.Digest),
                ("image3", "v2.3.3@sha256:000", BumpSize.Major, "v3.4.5@sha256:00d", BumpSize.Major),
                ("image3", "v2.3.3@sha256:000", BumpSize.Minor, "v2.4.5@sha256:012", BumpSize.Minor),
                ("image3", "v2.3.3@sha256:000", BumpSize.Patch, "v2.3.5@sha256:003", BumpSize.Patch),
                ("image3", "v2.3.3@sha256:000", BumpSize.Digest, "v2.3.3@sha256:002", BumpSize.Digest),
                ("image3", "v2.3.3@sha256:002", BumpSize.Major, "v3.4.5@sha256:00d", BumpSize.Major),
                ("image3", "v2.3.3@sha256:002", BumpSize.Minor, "v2.4.5@sha256:012", BumpSize.Minor),
                ("image3", "v2.3.3@sha256:002", BumpSize.Patch, "v2.3.5@sha256:003", BumpSize.Patch),
                ("image3", "v2.3.3@sha256:002", BumpSize.Digest),

                ("image7", "1", BumpSize.Major, "2@sha256:008", BumpSize.Major),
                ("image7", "1", BumpSize.Minor, "1@sha256:001", BumpSize.Digest),
                ("image7", "1@sha256:000", BumpSize.Major, "2@sha256:008", BumpSize.Major),
                ("image7", "1@sha256:000", BumpSize.Minor, "1@sha256:001", BumpSize.Digest),
                ("image7", "1@sha256:001", BumpSize.Major, "2@sha256:008", BumpSize.Major),
                ("image7", "1@sha256:001", BumpSize.Minor),

                ("image7", "1.1", BumpSize.Major, "2.2@sha256:00c", BumpSize.Major),
                ("image7", "1.1", BumpSize.Minor, "1.2@sha256:005", BumpSize.Minor),
                ("image7", "1.1", BumpSize.Patch, "1.1@sha256:002", BumpSize.Digest),
                ("image7", "1.1@sha256:000", BumpSize.Major, "2.2@sha256:00c", BumpSize.Major),
                ("image7", "1.1@sha256:000", BumpSize.Minor, "1.2@sha256:005", BumpSize.Minor),
                ("image7", "1.1@sha256:000", BumpSize.Patch, "1.1@sha256:002", BumpSize.Digest),
                ("image7", "1.1@sha256:002", BumpSize.Major, "2.2@sha256:00c", BumpSize.Major),
                ("image7", "1.1@sha256:002", BumpSize.Minor, "1.2@sha256:005", BumpSize.Minor),
                ("image7", "1.1@sha256:002", BumpSize.Patch),

                ("image7", "1.1.1", BumpSize.Major, "2.2.2@sha256:00d", BumpSize.Major),
                ("image7", "1.1.1", BumpSize.Minor, "1.2.2@sha256:007", BumpSize.Minor),
                ("image7", "1.1.1", BumpSize.Patch, "1.1.2@sha256:004", BumpSize.Patch),
                ("image7", "1.1.1", BumpSize.Digest, "1.1.1@sha256:003", BumpSize.Digest),
                ("image7", "1.1.1@sha256:000", BumpSize.Major, "2.2.2@sha256:00d", BumpSize.Major),
                ("image7", "1.1.1@sha256:000", BumpSize.Minor, "1.2.2@sha256:007", BumpSize.Minor),
                ("image7", "1.1.1@sha256:000", BumpSize.Patch, "1.1.2@sha256:004", BumpSize.Patch),
                ("image7", "1.1.1@sha256:000", BumpSize.Digest, "1.1.1@sha256:003", BumpSize.Digest),
                ("image7", "1.1.1@sha256:003", BumpSize.Major, "2.2.2@sha256:00d", BumpSize.Major),
                ("image7", "1.1.1@sha256:003", BumpSize.Minor, "1.2.2@sha256:007", BumpSize.Minor),
                ("image7", "1.1.1@sha256:003", BumpSize.Patch, "1.1.2@sha256:004", BumpSize.Patch),
                ("image7", "1.1.1@sha256:003", BumpSize.Digest),

                ("image6", "v2.3.4", BumpSize.Patch, "v2.3.5@sha256:006", BumpSize.Patch),
                ("image6", "v2.3.4", BumpSize.Minor, "v2.3.5@sha256:006", BumpSize.Patch),
                ("image6", "v2.3.4", BumpSize.Major, "v2.3.5@sha256:006", BumpSize.Patch),
                ("image6", "v2.3", BumpSize.Minor, "v2.4@sha256:008", BumpSize.Minor),
                ("image6", "v2", BumpSize.Major, "v4@sha256:00b", BumpSize.Major),
                ("image6", "v2.3.4-alpine", BumpSize.Patch, "v2.3.5-alpine@sha256:012", BumpSize.Patch),
                ("image6", "v2.3.4-alpine", BumpSize.Minor, "v2.3.5-alpine@sha256:012", BumpSize.Patch),
                ("image6", "v2.3-alpine", BumpSize.Minor, "v2.4-alpine@sha256:014", BumpSize.Minor),
                ("image6", "v2-alpine", BumpSize.Major, "v4-alpine@sha256:017", BumpSize.Major),
                ("image6", "v2.3.4-debian", BumpSize.Patch, "v2.3.5-debian@sha256:01e", BumpSize.Patch),
                ("image6", "v2.3.4-debian", BumpSize.Minor, "v2.3.5-debian@sha256:01e", BumpSize.Patch),
                ("image6", "v2.3-debian", BumpSize.Minor, "v2.4-debian@sha256:020", BumpSize.Minor),
                ("image6", "v2-debian", BumpSize.Major, "v4-debian@sha256:023", BumpSize.Major),
                ("image6", "v2.3.4@sha256:000", BumpSize.Digest, "v2.3.4@sha256:004", BumpSize.Digest),
                ("image6", "v2.3.4-alpine@sha256:000", BumpSize.Digest, "v2.3.4-alpine@sha256:010", BumpSize.Digest),
            }.Select(q =>
            {
                string name;
                Optional<string> currentTagAndDigest;
                BumpSize maxBumpSize;
                Optional<(string TagAndDigest, BumpSize BumpSize)> expectedResult;

                if (q.Is<(string Name, Optional<string> CurrentTagAndDigest, BumpSize MaxBumpSize)>(out var u1))
                {
                    name = u1.Name;
                    currentTagAndDigest = u1.CurrentTagAndDigest;
                    maxBumpSize = u1.MaxBumpSize;
                    expectedResult = new();
                }
                else
                {
                    var r = q.As<(string Name, Optional<string> CurrentTagAndDigest, BumpSize MaxBumpSize, string ExpectedTagAndDigest, BumpSize BumpSize)>().Value;
                    name = r.Name;
                    currentTagAndDigest = r.CurrentTagAndDigest;
                    maxBumpSize = r.MaxBumpSize;
                    expectedResult = (r.ExpectedTagAndDigest, r.BumpSize);
                }

                return new object[] { name, currentTagAndDigest, maxBumpSize, tagsByName, digestsByNameAndTag, expectedResult };
            });
        }


        [Theory]
        [MemberData(nameof(GetTestData))]
        public async Task WillSelectCorrectUpdateTarget(string name, Optional<string> currentTagAndDigest, BumpSize maxBumpSize, Dictionary<string, List<string>> tagsByName, Dictionary<string, string> digestsByNameAndTag, Optional<(string TagAndDigest, BumpSize BumpSize)> expectedResult)
        {
            var sut = GetSut(new FakeSkopeoService(tagsByName, digestsByNameAndTag));
            var currentFullImage = currentTagAndDigest.As(q => $"{name}:{q}").Or(name);
            var becauseString = $"the image was {currentFullImage} with maxBump {maxBumpSize}";
            var result = await sut.SelectUpdateTarget(currentFullImage, maxBumpSize);
            result.HasValue.Should().Be(expectedResult.HasValue, becauseString);

            if (result.HasValue)
            {
                result.Value.NewImage.ToString().Should().Be($"{name}:{expectedResult.Value.TagAndDigest}", becauseString);
                result.Value.BumpSize.Should().Be(expectedResult.Value.BumpSize, becauseString);
            }
        }


    }
}
