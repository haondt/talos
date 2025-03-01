namespace Talos.Core.Models
{
    public static class RedisNamespacer
    {
        public static class Pushes
        {
            private static readonly string Segment = "pushes";
            public static class Timestamps
            {
                private static readonly string Segment = $"{Pushes.Segment}:timestamps";
                public static string Domain(string domain) => $"{Segment}:domain:{domain}";
                public static string Repo(string repo) => $"{Segment}:repo:{repo}";
            }

            public static readonly string Queue = $"{Segment}:queue";
            public static string Push(string id) => $"{Segment}:push:{id}";
            public static class DeadLetters
            {
                private static readonly string Segment = $"{Pushes.Segment}:deadletters";
                public static string DeadLetter(string id) => $"{Segment}:deadletter:{id}";
                public static readonly string Queue = $"{Segment}:queue";
            }
        }

        public static string UpdateTarget(string id) => $"target:{id}";
        public static class Skopeo
        {
            private static readonly string Segment = "skopeo";
            public static string Tags(string id) => $"{Segment}:tags:{id}";
            public static string Inspect(string id) => $"{Segment}:inspect:{id}";
        }

        public static class Git
        {
            private static readonly string Segment = "git";
            public static readonly string Commits = $"{Segment}:commits";
        }

        public static class Webhooks
        {
            private static readonly string Segment = "webhooks";
            public static class Tokens
            {
                private static readonly string Segment = $"{Webhooks.Segment}:tokens";
                public static string ByName => $"{Segment}:byname";
                public static string ByValue => $"{Segment}:byvalue";
            }
        }
    }
}
