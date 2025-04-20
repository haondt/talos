using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Talos.Core.Models;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Dockerfile.Models;
using Talos.ImageUpdate.Repositories.Dockerfile.Services;
using Talos.ImageUpdate.Shared.Models;
using Talos.ImageUpdate.Tests.Hosting;

namespace Talos.ImageUpdate.Tests
{
    [Collection(nameof(TestServiceCollection))]
    public class DockerfileFileServiceTests(TestServiceFixture fixture)
    {
        [Fact]
        public void WillNotMessWithOtherFileData()
        {
            var originalText = @"# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY [""Elysium/Elysium.csproj"", ""Elysium/""]
COPY [""nuget.config"", "".""]
RUN dotnet restore ""./Elysium/Elysium.csproj""
COPY . .
WORKDIR ""/src/Elysium""
RUN dotnet build ""./Elysium.csproj"" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish ""./Elysium.csproj"" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [""dotnet"", ""Elysium.dll""]
";
            var expectedText = @"# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM newimage:newtag AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY [""Elysium/Elysium.csproj"", ""Elysium/""]
COPY [""nuget.config"", "".""]
RUN dotnet restore ""./Elysium/Elysium.csproj""
COPY . .
WORKDIR ""/src/Elysium""
RUN dotnet build ""./Elysium.csproj"" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish ""./Elysium.csproj"" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [""dotnet"", ""Elysium.dll""]
";
            var result = DockerfileFileService.SetFromImage(originalText, 10, "newimage:newtag");
            result.Value.NewFileContents.Should().Be(expectedText);
        }

        [Fact]
        public void WithThrowOnMissingFromLine()
        {
            var originalText = @"FROM foo:bar
WORKDIR /app
";

            var result = DockerfileFileService.SetFromImage(originalText, 1, "newimage:newtag");
            result.IsSuccessful.Should().BeFalse();
        }

        [Fact]
        public void WillExtractLocations()
        {
            var originalText = @"# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# !talos sync.role=Parent
# !talos sync.group=some_group
# !talos sync.id=some_parent_image
# !talos bump=Patch strategy.digest=Push strategy.patch=Notify
USER app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base2
USER app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base3
# !talos: x
USER app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base4
# !talos skip=true
USER app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base5
# !talos sync.role=Child sync.group=some_group
# !talos sync.id=some_child_image
USER app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base6
# !talos: +!
USER app
";

            var sut = ActivatorUtilities.CreateInstance<DockerfileFileService>(fixture.ServiceProvider);
            var locations = sut.ExtractLocations("./Dockerfile", originalText);
            var lines = originalText.Split(Environment.NewLine);

            var parsedImage = new ParsedImage("aspnet", "mcr.microsoft.com/dotnet/aspnet")
            {
                Domain = "mcr.microsoft.com",
                Namespace = "dotnet",
                TagAndDigest = new(new()
                {
                    Tag = new(new SemanticVersion
                    {
                        Major = 8,
                        Minor = 0
                    })
                }),
            };

            locations.Should().HaveCount(4);
            locations[0].Value.Should().BeEquivalentTo(new DockerfileUpdateLocation()
            {
                Coordinates = new()
                {
                    Line = 3,
                    RelativeFilePath = "./Dockerfile"
                },
                State = new()
                {
                    Configuration = new()
                    {
                        Bump = BumpSize.Patch,
                        Skip = false,
                        Strategy = new()
                        {
                            Digest = BumpStrategy.Push,
                            Patch = BumpStrategy.Notify
                        },
                        Sync = new()
                        {
                            Role = SyncRole.Parent,
                            Group = "some_group",
                            Id = "some_parent_image"
                        }
                    },
                    Snapshot = new()
                    {
                        CurrentImage = parsedImage,
                        LineHash = HashUtils.ComputeSha256Hash(lines[3])
                    }
                }
            });
            locations[1].IsSuccessful.Should().BeFalse();
            locations[2].Value.Should().BeEquivalentTo(new DockerfileUpdateLocation()
            {
                Coordinates = new()
                {
                    Line = 21,
                    RelativeFilePath = "./Dockerfile"
                },
                State = new()
                {
                    Configuration = new()
                    {
                        Skip = false,
                        Sync = new()
                        {
                            Role = SyncRole.Child,
                            Group = "some_group",
                            Id = "some_child_image"
                        }
                    },
                    Snapshot = new()
                    {
                        CurrentImage = parsedImage,
                        LineHash = HashUtils.ComputeSha256Hash(lines[21])
                    }
                }
            });
            locations[3].Value.Should().BeEquivalentTo(new DockerfileUpdateLocation()
            {
                Coordinates = new()
                {
                    Line = 26,
                    RelativeFilePath = "./Dockerfile"
                },
                State = new()
                {
                    Configuration = new()
                    {
                        Bump = BumpSize.Major,
                        Strategy = new()
                        {
                            Digest = BumpStrategy.Push,
                            Patch = BumpStrategy.Push,
                            Minor = BumpStrategy.Push,
                            Major = BumpStrategy.Push
                        },
                    },
                    Snapshot = new()
                    {
                        CurrentImage = parsedImage,
                        LineHash = HashUtils.ComputeSha256Hash(lines[26])
                    }
                }
            });

        }

    }

}
