using Haondt.Core.Models;

namespace Talos.Api.Extensions
{
    public static class ObjectExtensions
    {
        public static Optional<T> AsOptional<T>(this T? value) where T : struct
        {
            if (!value.HasValue)
                return new();
            return new(value.Value);
        }

        public static Optional<T> AsOptional<T>(this T? value) where T : notnull
        {
            if (value == null)
                return new();
            return new(value);
        }
    }
}
