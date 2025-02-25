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
                mockRedisProvider.Object);
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
                    ("v2.3.4", "sha256:003"),
                    ("v2.3.3", "sha256:003"),
                    ("v2.3.5", "sha256:003"),
                    ("v2.3", "sha256:003"),
                    ("v2.4", "sha256:003"),
                    ("v2.2", "sha256:003"),
                    ("v3", "sha256:003"),
                    ("v4", "sha256:003"),
                    ("v2", "sha256:003"),
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
                ]

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
                result.Value.DesiredImage.Should().Be($"{name}:{expectedResult.Value.TagAndDigest}", becauseString);
                result.Value.BumpSize.Should().Be(expectedResult.Value.BumpSize, becauseString);
            }

        }
    }
}
