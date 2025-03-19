using Castle.DynamicProxy;
using Haondt.Core.Models;
using Talos.Core.Abstractions;
using Talos.Core.Models;

namespace Talos.Core.Extensions
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

        public static Result<T> AsResult<T>(this T? value) where T : struct
        {
            if (!value.HasValue)
                return new();
            return new(value.Value);
        }

        public static Result<T> AsResult<T>(this T? value) where T : notnull
        {
            if (value == null)
                return new();
            return new(value);
        }

        private static ProxyGenerator _proxyGenerator = new();
        public static T WithMethodTracing<T>(this T obj, ITracer tracer) where T : class
        {
            return _proxyGenerator.CreateInterfaceProxyWithTarget(obj, new TracingInterceptor(tracer));
        }
    }
}
