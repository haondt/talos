using FluentAssertions;
using Talos.ImageUpdate.Repositories.DockerCompose.Services;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Tests
{
    public class DockerComposeFileTests
    {
        [Fact]
        public void WillNotMessWithOtherYamlData()
        {
            var originalYaml = @"version: '3'
# test
x-custom:
  - foo:

    - bar:
      - baz:
    - bar:
      - baz:

      - qux:
      - foo:
services:
  other_app:
    image: other_image
  app:
    networks:
      net1:
    image: oldimage:oldtag
    volumes:
      - ./foo/bar:/baz/qux
      - some:/thing
  # deploy:
  #   replicas: 2
    environment:
      FOO_BAR: 100
      BAZ_QUX: ""100""
      QUUX: '1001'
      CORGE: abcdef
      GRAULT: true
      GARPLY: false
      WALDO: yes
      FRED: no
      PLUGH: 1
      XYZZY: 0
      THUD:
      THUD_v2: null
      
  other-app-2:
    image: &xyz other-image-2:latest@sha256:000
  other-app-3:
    image: *xyz
  other_app_4:
    image: other_image

networks:
  net1:
volumes:
  vol1:
  vol2:
";
            var expectedYaml = @"version: '3'
# test
x-custom:
  - foo:

    - bar:
      - baz:
    - bar:
      - baz:

      - qux:
      - foo:
services:
  other_app:
    image: other_image
  app:
    networks:
      net1:
    image: newimage:newtag
    volumes:
      - ./foo/bar:/baz/qux
      - some:/thing
  # deploy:
  #   replicas: 2
    environment:
      FOO_BAR: 100
      BAZ_QUX: ""100""
      QUUX: '1001'
      CORGE: abcdef
      GRAULT: true
      GARPLY: false
      WALDO: yes
      FRED: no
      PLUGH: 1
      XYZZY: 0
      THUD:
      THUD_v2: null
      
  other-app-2:
    image: &xyz other-image-2:latest@sha256:000
  other-app-3:
    image: *xyz
  other_app_4:
    image: other_image

networks:
  net1:
volumes:
  vol1:
  vol2:
";
            var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "newimage:newtag");
            result.Value.NewFileContents.Should().Be(expectedYaml);
        }

        [Fact]
        public void WithThrowOnMissingServices()
        {
            var originalYaml = @"services:
  app:
    image: oldimage:oldtag
";

            var result = DockerComposeFileService.SetServiceImage(originalYaml, "nonexistent", "newimage:newtag");
            result.IsSuccessful.Should().BeFalse();
        }

        [Fact]
        public void WillThrowOnAnchors()
        {
            var originalYaml = @"services:
  app:
    image: &foo oldimage:oldtag
";

            var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "newimage:newtag");
            result.IsSuccessful.Should().BeFalse();
        }
        [Fact]
        public void WillThrowOnAnchorRefs()
        {
            var originalYaml = @"services:
  app:
    image: *foo
";

            var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "newimage:newtag");
            result.IsSuccessful.Should().BeFalse();
        }

        [Fact]
        public void WillIgnoreOtherTopLevelKeys()
        {
            var originalYaml = @"x-other-thing:
  app:
    image: foo
services:
  foo:
    image: foo
x-yet-another-thing:
  app:
    image: foo
";
            var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "bar");
            result.IsSuccessful.Should().BeFalse();

        }

        [Fact]
        public void WillIgnoreOtherServices()
        {
            var originalYaml = @"services:
  app2:
    image: foo
  app:
    image: foo
  app3:
    image: foo
";
            var expectedYaml = @"services:
  app2:
    image: foo
  app:
    image: bar
  app3:
    image: foo
";
            var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "bar");
            result.Value.NewFileContents.Should().Be(expectedYaml);
        }

        [Fact]
        public void WillRespectKeyHierarchy()
        {
            var originalYaml = @"services:
  app:
    build:
      context: .
x-other:
  app:
    image: foo
";
            var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "bar");
            result.IsSuccessful.Should().BeFalse();
        }

        [Fact]
        public void WillIgnoreComments()
        {
            var originalYaml = @"services:
  # app:
  #  build:
  #    context: .
#x-other:
  app:
    image: foo
";
            var expectedYaml = @"services:
  # app:
  #  build:
  #    context: .
#x-other:
  app:
    image: bar
";
            var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "bar");
            result.Value.NewFileContents.Should().Be(expectedYaml);
        }

        [Theory]
        [InlineData("*foo", false)]
        [InlineData(" *foo", false)]
        [InlineData("  *foo", false)]
        [InlineData("* foo", false)]
        [InlineData(" * foo", false)]
        [InlineData("  * foo", false)]
        [InlineData("&foo", false)]
        [InlineData(" &foo", false)]
        [InlineData("  &foo", false)]
        [InlineData("& foo", false)]
        [InlineData(" & foo", false)]
        [InlineData("  & foo", false)]
        [InlineData("#foo", false)]
        [InlineData(" #foo", false)]
        [InlineData("  #foo", false)]
        [InlineData("# foo", false)]
        [InlineData(" # foo", false)]
        [InlineData("  # foo", false)]
        [InlineData("foo", true)]
        [InlineData(" foo", true)]
        [InlineData("  foo", true)]
        [InlineData("*foo baz", false)]
        [InlineData(" *foo baz", false)]
        [InlineData("  *foo baz", false)]
        [InlineData("* foo baz", false)]
        [InlineData(" * foo baz", false)]
        [InlineData("  * foo baz", false)]
        [InlineData("&foo baz", false)]
        [InlineData(" &foo baz", false)]
        [InlineData("  &foo baz", false)]
        [InlineData("& foo baz", false)]
        [InlineData(" & foo baz", false)]
        [InlineData("  & foo baz", false)]
        [InlineData("#foo baz", false)]
        [InlineData(" #foo baz", false)]
        [InlineData("  #foo baz", false)]
        [InlineData("# foo baz", false)]
        [InlineData(" # foo baz", false)]
        [InlineData("  # foo baz", false)]
        [InlineData("foo baz", false)]
        [InlineData(" foo baz", false)]
        [InlineData("  foo baz", false)]
        public void WillCorrectlyFilterValidImageKeys(string originalTag, bool shouldSucceed)
        {
            var originalYaml = $@"services:
  app:
    image:{originalTag}
";
            var expectedYaml = @"services:
  app:
    image: bar
";
            if (shouldSucceed)
            {

                var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "bar");
                result.Value.NewFileContents.Should().Be(expectedYaml);
            }
            else
            {
                var result = DockerComposeFileService.SetServiceImage(originalYaml, "app", "bar");
                result.IsSuccessful.Should().BeFalse();
            }
        }

        [Fact]
        public void WillUpdateImageCorrectly()
        {
            var originalYaml = @"



services:
  elysium-stage:
    networks:
      - nginx
      - elysium-stage
    image: registry.gitlab.com/haondt/cicd/registry/elysium:0.0.5
    depends_on:
      - elysium-stage-silo
    environment:
      VIRTUAL_HOST: elysium-stage.haondt.dev
      VIRTUAL_PORT: 8080
    env_file:
      - ./elysium-stage/elysium.env
      - ./elysium-stage/shared.env
  elysium-stage-silo:
    networks:
      - nginx
      - elysium-stage
    image: registry.gitlab.com/haondt/cicd/registry/elysium-silo:0.0.5
    depends_on:
      - elysium-stage-postgres
      - elysium-stage-redis
    environment:
      VIRTUAL_HOST: elysium-stage-silo.chert
      VIRTUAL_PORT: 8080
    env_file:
      - ./elysium-stage/shared.env
    # deploy:
    #   replicas: 2
  elysium-stage-postgres:
    networks:
      - elysium-stage
      - wireguard
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: ""{{ elysium__stage__postgres__user }}""
      POSTGRES_PASSWORD: ""{{ elysium__stage__postgres__password }}""
      PGDATA: /data/pgdata
    volumes:
      - elysium-postgres-data:/data
      - ./elysium-stage/postgresql-init.sql:/docker-entrypoint-initdb.d/postgresql-init.sql
  elysium-stage-redis:
    image: redis
    networks:
      - elysium-stage

networks:
  elysium-stage:

volumes:
  elysium-postgres-data:

";
            var expectedYaml = @"



services:
  elysium-stage:
    networks:
      - nginx
      - elysium-stage
    image: registry.gitlab.com/haondt/cicd/registry/elysium:0.1.0@sha256:ab42ae6871d9a12b90c7a259f808aade4d84496317a7e38621d7ebca07fc02f6
    depends_on:
      - elysium-stage-silo
    environment:
      VIRTUAL_HOST: elysium-stage.haondt.dev
      VIRTUAL_PORT: 8080
    env_file:
      - ./elysium-stage/elysium.env
      - ./elysium-stage/shared.env
  elysium-stage-silo:
    networks:
      - nginx
      - elysium-stage
    image: registry.gitlab.com/haondt/cicd/registry/elysium-silo:0.1.0@sha256:ab42ae6871d9a12b90c7a259f808aade4d84496317a7e38621d7ebca07fc02f6
    depends_on:
      - elysium-stage-postgres
      - elysium-stage-redis
    environment:
      VIRTUAL_HOST: elysium-stage-silo.chert
      VIRTUAL_PORT: 8080
    env_file:
      - ./elysium-stage/shared.env
    # deploy:
    #   replicas: 2
  elysium-stage-postgres:
    networks:
      - elysium-stage
      - wireguard
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: ""{{ elysium__stage__postgres__user }}""
      POSTGRES_PASSWORD: ""{{ elysium__stage__postgres__password }}""
      PGDATA: /data/pgdata
    volumes:
      - elysium-postgres-data:/data
      - ./elysium-stage/postgresql-init.sql:/docker-entrypoint-initdb.d/postgresql-init.sql
  elysium-stage-redis:
    image: redis
    networks:
      - elysium-stage

networks:
  elysium-stage:

volumes:
  elysium-postgres-data:

";

            var result = DockerComposeFileService.SetServiceImage(originalYaml, "elysium-stage", "registry.gitlab.com/haondt/cicd/registry/elysium:0.1.0@sha256:ab42ae6871d9a12b90c7a259f808aade4d84496317a7e38621d7ebca07fc02f6");
            result = DockerComposeFileService.SetServiceImage(result.Value.NewFileContents, "elysium-stage-silo", "registry.gitlab.com/haondt/cicd/registry/elysium-silo:0.1.0@sha256:ab42ae6871d9a12b90c7a259f808aade4d84496317a7e38621d7ebca07fc02f6");
            result.Value.NewFileContents.Should().Be(expectedYaml);
        }


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
