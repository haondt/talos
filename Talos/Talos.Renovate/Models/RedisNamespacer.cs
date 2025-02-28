namespace Talos.Renovate.Models
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
        }

        public static string UpdateTarget(string id) => $"target:{id}";
        public static class Skopeo
        {
            private static readonly string Segment = "skopeo";
            public static string Tags(string id) => $"{Segment}:tags:{id}";
            public static string Inspect(string id) => $"{Segment}:inspect:{id}";
        }
    }
}
