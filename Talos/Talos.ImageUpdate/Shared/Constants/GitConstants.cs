namespace Talos.ImageUpdate.Shared.Constants
{
    public static class GitConstants
    {
        private const string UPDATES_BRANCH_NAME_PREFIX = "talos/updates";
        public static string GetUpdatesBranchName(string targetBranchName) => $"{UPDATES_BRANCH_NAME_PREFIX}-{targetBranchName}";
    }
}
