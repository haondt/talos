using FluentAssertions;
using Microsoft.Extensions.Options;
using Talos.Renovate.Models;

namespace Talos.Renovate.Tests
{
    public class ImageParserTests
    {
        public static IEnumerable<object[]> GetTestData()
        {
            return new List<(string, ParsedImage)>
            {
                ("alpine", new ParsedImage(Name: "alpine", Untagged: "alpine")),
                ("alpine:latest", new ParsedImage(Name: "alpine", Untagged: "alpine", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new("latest"))))),
                ("_/alpine", new ParsedImage(Name: "alpine", Untagged: "_/alpine", Namespace: "_")),
                ("_/alpine:latest", new ParsedImage(Name: "alpine", Untagged: "_/alpine", Namespace: "_", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new("latest"))))),
                ("alpine:3.7", new ParsedImage(Name: "alpine", Untagged: "alpine", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new(new SemanticVersion(Major: 3, Minor: 7)))))),
                ("docker.example.com/gmr/alpine:3.7", new ParsedImage(Name: "alpine", Untagged: "docker.example.com/gmr/alpine", Domain: "docker.example.com", Namespace: "gmr", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new SemanticVersion(Major: 3, Minor: 7))))),
                ("docker.example.com:5000/gmr/alpine:latest", new ParsedImage(Name: "alpine", Untagged: "docker.example.com:5000/gmr/alpine", Domain: "docker.example.com:5000", Namespace: "gmr", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new ("latest"))))),
                ("pse/anabroker:v1-alpine@sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854", new ParsedImage(Name: "anabroker", Namespace: "pse", Untagged: "pse/anabroker", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new SemanticVersion(VersionPrefix: "v", Major: 1), Variant: "alpine"), Digest: "sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854"))),

                ("pse/anabroker:latest-alpine@sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854", new ParsedImage(Name: "anabroker", Namespace: "pse", Untagged: "pse/anabroker", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new ("latest"), Variant: "alpine"), Digest: "sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854"))),
                ("registry.gitlab.com/haondt/cicd/registry/gabbro-bot:v1-alpine", new ParsedImage(Name: "gabbro-bot", Namespace: "haondt/cicd/registry", Domain: "registry.gitlab.com", Untagged: "registry.gitlab.com/haondt/cicd/registry/gabbro-bot", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new SemanticVersion (VersionPrefix: "v", Major: 1), Variant: "alpine")))),
                ("nginxproxy/nginx-proxy:v1.0.0@sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854", new ParsedImage(Name: "nginx-proxy", Namespace: "nginxproxy", Untagged: "nginxproxy/nginx-proxy", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new SemanticVersion(VersionPrefix: "v", Major: 1, Minor: 0, Patch: 0)), Digest: "sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854"))),
                ("lscr.io/linuxserver/fail2ban:latest@sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854", new ParsedImage(Name: "fail2ban", Domain: "lscr.io", Namespace: "linuxserver", Untagged: "lscr.io/linuxserver/fail2ban", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new ("latest")), Digest: "sha256:e4b1c4c01080ffdf981dbd4dac1a1230b857624c043057ecfee3ff536275854"))),
                ("ghcr.io/immich-app/immich-server:v1.0.3", new ParsedImage(Name: "immich-server", Domain: "ghcr.io", Namespace: "immich-app", Untagged: "ghcr.io/immich-app/immich-server", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new SemanticVersion(VersionPrefix: "v", Major: 1, Minor: 0, Patch: 3))))),
                ("lscr.io/linuxserver/wireguard:1.2", new ParsedImage(Name: "wireguard", Domain: "lscr.io", Namespace: "linuxserver", Untagged: "lscr.io/linuxserver/wireguard", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new SemanticVersion(Major: 1, Minor: 2))))),
                ("redis:1", new ParsedImage(Name: "redis", Untagged: "redis", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new SemanticVersion(Major: 1))))),
                ("redis", new ParsedImage(Name: "redis", Untagged: "redis")),
                ("nginx:stable-alpine", new ParsedImage(Name: "nginx", Untagged: "nginx", TagAndDigest: new ParsedTagAndDigest(new ParsedTag(Version: new("stable"), Variant: "alpine")))),
            }.Select(q => new object[] { q.Item1, q.Item2 });
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void WillParseImages(string stringImage, ParsedImage parsedImage)
        {
            var parser = new ImageParser(Options.Create(new ImageParserSettings()));
            parser.Parse(stringImage, false).Should().BeEquivalentTo(parsedImage);
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void WillUnparseImages(string stringImage, ParsedImage parsedImage)
        {
            parsedImage.ToString().Should().Be(stringImage);
        }

        [Theory]
        [InlineData("alpine", "docker.io/library/alpine")]
        [InlineData("library/alpine:latest", "docker.io/library/alpine:latest")]
        [InlineData("redis", "docker.io/library/redis")]
        [InlineData("haumea/charon", "docker.io/haumea/charon")]
        [InlineData("docker.io/haumea/charon", "docker.io/haumea/charon")]
        [InlineData("lscr.io/linuxserver/wireguard", "lscr.io/linuxserver/wireguard")]
        [InlineData("my-registry.com/my-image", "my-registry.com/my-image")]
        public void WillInsertDefaultDomain(string inputImage, string outputImage)
        {
            var parser = new ImageParser(Options.Create(new ImageParserSettings()));
            parser.Parse(inputImage, true).ToString().Should().BeEquivalentTo(outputImage);
        }
    }
}