# Talos

A Discord bot for managing docker containers on my home server.

## Deployment

Talos wraps a few command line tools that must be installed on the host system:

- [`docker`](https://docs.docker.com/reference/cli/docker/)
- [`skopeo`](https://github.com/containers/skopeo)
- [`git`](https://git-scm.com/)

The easiest way to run Talos is with the docker image, with has these utilities pre-installed.
Talos also requires a Redis instance for caching, so the recommended way to get up and running is with Docker Compose.

```yml
services:
  talos:
    image: registry.gitlab.com/haondt/cicd/registry/talos:latest
    user: talos
    group_add:
      - 1234 # talos needs to be part of the docker group
    networks:
      - talos
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    environment:
        RedisSettings__Endpoint: talos-redis:6379
        DiscordSettings__BotToken: your-bot-token
        DiscordSettings__GuildId: 1234
        DiscordSettings__ChannelId: 1234
  talos-redis:
    networks:
      - talos
    image: redis

networks:
  talos:
```

## Features & Configuration

Talos is configured using .NET appsettings, meaning it can be configured through environment variables or by mounting an `appsettings.Production.json`
in the `/app` directory.

### Discord Integration

Talos uses Discord as a frontend. It will respond to slash commands and send notifications.
Talos will only work in the provided guild and will default to the provided channel for messaging.

```json
{
    "DiscordSettings": {
        "BotToken": "your-bot-token",
        "GuildId": 1234567890
        "ChannelId": 1234567890
    }
}
```

<div align="center">
    <img src="docs/images/discord.png">
</div>


### Docker

Talos can connect to docker hosts both locally and remotely to run docker operations.
For local hosts, simply add an entry in the `DockerSettings` with the docker version.

The version sets which `compose` command Talos will use, i.e. V1 = `docker-compose`, V2 = `docker compose`.

```json
{
    "DockerSettings": {
        "Hosts": {
            "localhost": {
                "DockerVersion": "V2",
            }
        }
    }
}
```

With this, Talos can execute commands on the host.

<div align="center">
    <img src="docs/images/docker1.png">
</div>
<div align="center">
    <img src="docs/images/docker2.png">
</div>

#### Remote Hosts

Talos also supports hosts on remote machines over ssh.
To use this, Talos must have access to an identity file.

```json
{
    "DockerSettings": {
        "Hosts": {
            "remote_machine": {
                "DockerVersion": "V2",
                "SSHConfig": {
                    "Host": "163.174.249.51",
                    "User": "talos",
                    "IdentityFile": "/config/id_rsa"
                }
            }
        }
    }
}
```

> [!IMPORTANT]
> Talos runs as the `talos` user in it's container, and uses the configured user on remote hosts.
> This user must exist on the host, and must belong to the docker group in order for Talos to run docker commands.

### Image Updating

Talos can automatically update docker images in an IaC repository, sort of like [Renovate](https://www.mend.io/renovate/).
To configure it, you must first tell Talos how often to run scans, and what repositories to connect to.

The schedule option defines how often to run.

```json
{
    "ImageUpdateSettings": {
        "Schedule": { // every hour
            "Type": "Delay",
            "DelaySeconds": 3600
        }
    }
}
```

Each repository references a host, which provides info on how to connect and authenticate with it.

```json
{
    "ImageUpdateSettings": {
        "Hosts": {
            "gitlab": {
                "Type": "GitLab",
                "Name": "oauth2", // username
                "Token": "glpt-abcabcabc", // personal access token
                "Email": "talos@example.com" // email for the commits
            }
        },
        "Repositories": [
            {
                "Url": "https://gitlab.com/haondt/my-repository",
                "Host": "gitlab", // references Hosts section
                "Branch": "main",
                "IncludeGlobs": [ // globs of docker compose files to watch
                    "docker-compose.yml",
                    "docker-compose.*.yml",
                ],
                "CooldownSeconds": 300
            }
        ]
    }
}
```

> [!NOTE]
> The idea here is that Talos will commit to a repository, then a deployment pipeline takes over to push the update.
> To avoid creating too many pipelines at once, Talos allows for a per-repository "Cooldown". After making a change to
> a repository, it will queue future changes until this cooldown passes.

Talos will check if images can be updated and commit the change to the repository, with a message detailing the changes.

<div align="center">
    <img src="docs/images/commit.png">
</div>

#### Registry Throttling

Some container registries have limits on how often you can pull from them. You can feed this info to Talos,
and it will limit how often it pushes an update that will cause a pull from one of those image registries.

```json
{
    "UpdateThrottlingSettings": {
        "Domains": {
            "docker.io": {
                "Limit": 5,
                "Per": "Hour"
            }
        }
    }
}
```

#### Update Strategy

The update strategy is configured per-container in the docker compose file.

```yml
services:
  helloworld:
    image: hello-world
    x-talos:
      skip: false # set to true to tell Talos to ignore this container
      bump: minor
      strategy:
        digest: push
        patch: push
        minor: prompt
        major: skip
```

Talos looks for the `x-talos` extension for info on how to update the image. If the options have `skip: true`, then Talos will ignore this container.

The `strategy` section describes how to handle updates of varying sizes. The sizes are as follows:

- `digest`: when a tag has the name name but the digest has been updated. For named tags (e.g. `latest`), the update is always `digest`.
- `patch`, `minor`, `major`: the respective component of a tag formatted as a semantic version

Each update **size** is mapped to a **strategy**. The strategies are as follows:

- `push`: Push the change directly to the branch
- `prompt`: Send a Discord notification asking how to handle the update
- `notify`: Send a Discord notification saying that there is a new version available
- `skip`: Ignore the new version entirely

Lastly, the `bump` section describes the maximum size to consider for an update. For example, if an image is at `v1.2.3`, and `v1.3.0` is released, Talos will
only bump the version if the `bump` size is `minor` or `major`. Additionally, Talos will only consider tags with the same number of semantic segments.
So `v1.2.3` can be bumped to `v1.3.0`, but not to `v1.3`.

<div align="center">
    <img src="docs/images/prompt.png">
</div>

**Compact Form**

For convenience, Talos can also respond to a "compact form" extension, `x-tl`.
This extension takes the form of a single string, `<bump>:<strategies>`. More precisely:

- if the extension is just `x`, then skip
- otherwise, 1st character is the max bump size (`+`, `^`, `~`, `@` for `major`, `minor`, `patch`, `digest`)
- if there is no second character use `notify` for all sizes
- if the second character is not a colon (`:`), then it is the strategy to use for all sizes
- the strategies are indicated as `*`, `?`, `.`, `!` for `notify`, `prompt`, `skip`, `push`
- if the second character is a colon, the following characters specify the strategy for the
  `digest`, `patch`, `minor` and `major` in that order. If there are less than 4 characters given,
  then we assume `prompt` for the missing ones

The previous example could be written in the compact form as such:

```yml
services:
  helloworld:
    image: hello-world
    x-tl: ^:!!?.
```

Some more examples:

```yml
x-tl: ~ # bump size patch, all notify
x-tl: +! # bump size major, all push
x-tl: ^? # bump size minor, prompt all
x-tl: ~:!? # bump size patch, digest -> push, patch -> prompt
x-tl: @:!!!! # bump size digest, digest -> push, everything else is push but will be ignored because the max is digest
x-tl: +:! # bump size major, digest = push, everything else is the default (notify)
```

#### Dead Lettering

Since Talos is pushing updates on a schedule, and may delay pushes in accordance with the various rate-limiting mechanisms, the even that causes a push
and the push itself are asynchronous. Therefore, when Talos puts an update into the queue, it considers it "pushed". If the push fails, the push goes into
a dead letter queue. On the next update run Talos will notice the disparity between what it thought it pushed and whats in the repo, and will re-enqueue a new push. You don't have to worry about managing the dead letter queue, but you can examine it to see which pushes failed, why, and if desired you can replay
them without waiting for the next scheduled update.

Talos will never downgrade an image, even if you are replaying an old dead letter.

<div align="center">
    <img src="docs/images/deadletters.png">
</div>

### Webhooks

After pushing an update, Talos can listen for pipeline events, and if a pipeline completes that references one of Talos' commits,
it will notify the Discord channel.

The only configuration needed is the base url Talos should use to generate webhooks.

```json
{
    "ApiSettings": {
        "BaseUrl": "https://mydomain.com"
    }
}
```

<div align="center">
    <img src="docs/images/webhooks1.png">
</div>

<div align="center">
    <img src="docs/images/webhooks2.png">
</div>