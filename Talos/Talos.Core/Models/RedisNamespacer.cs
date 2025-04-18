using Haondt.Core.Models;

namespace Talos.Core.Models
{
    public static class RedisNamespacer
    {
        public static string SanitizeId(string id) => id.Replace("_", "__").Replace(':', '_');
        public const string Version = "talos:v2";
        public static class Pushes
        {
            private static readonly string Segment = $"{Version}:pushes";
            public static class Timestamps
            {
                private static readonly string Segment = $"{Pushes.Segment}:timestamps";
                public static string Domain(string domain) => $"{Segment}:domain:{SanitizeId(domain)}";
                public static string Repo(string repo, Optional<string> branch)
                {
                    repo = SanitizeId(repo);
                    if (branch.HasValue)
                        return $"{Segment}:repo:{repo}/{SanitizeId(branch.Value)}";
                    return $"{Segment}:repo:{repo}";
                }
            }

            public static readonly string Queue = $"{Segment}:queue";
            public static string Push(string id) => $"{Segment}:push:{SanitizeId(id)}";
            public static class DeadLetters
            {
                private static readonly string Segment = $"{Pushes.Segment}:deadletters";
                public static string DeadLetter(string id) => $"{Segment}:deadletter:{SanitizeId(id)}";
                public static readonly string Queue = $"{Segment}:queue";
            }
        }

        public static string UpdateTarget(string id) => $"{Version}:target:{SanitizeId(id)}";
        public static class Skopeo
        {
            private static readonly string Segment = $"{Version}:skopeo";
            public static string Tags(string id) => $"{Segment}:tags:{SanitizeId(id)}";
            public static string Inspect(string id) => $"{Segment}:inspect:{SanitizeId(id)}";
        }

        public static class Git
        {
            private static readonly string Segment = $"{Version}:git";
            public static readonly string Commits = $"{Segment}:commits";
        }

        public static class Webhooks
        {
            private static readonly string Segment = $"{Version}:webhooks";
            public static class Tokens
            {
                private static readonly string Segment = $"{Webhooks.Segment}:tokens";
                public static string ByName => $"{Segment}:byname";
                public static string ByValue => $"{Segment}:byvalue";
            }
        }

        public static class Discord
        {
            private static readonly string Segment = $"{Version}:discord";
            public static class Interaction
            {
                private static readonly string Segment = $"{Discord.Segment}:interaction";

                public static string ImageUpdate(string id) => $"{Segment}:imageupdate:{SanitizeId(id)}";
                public static class Component
                {
                    private static readonly string Segment = $"{Interaction.Segment}:component";
                    public static string Message(string id) => $"{Segment}:message:{SanitizeId(id)}";
                }
            }

        }
    }
}
