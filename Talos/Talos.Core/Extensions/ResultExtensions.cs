using Haondt.Core.Models;

namespace Talos.Core.Extensions
{
    public static class ResultExtensions
    {
        public static T Or<T>(this Result<T> result, T defaultValue) where T : notnull
        {
            if (result.IsSuccessful)
                return result.Value;
            return defaultValue;
        }

        public static T Or<T>(this Result<T> result, Func<T> defaultValueFactory) where T : notnull
        {
            if (result.IsSuccessful)
                return result.Value;

            return defaultValueFactory();
        }
    }
}
