using Castle.DynamicProxy;
using System.Reflection;
using Talos.Core.Abstractions;

namespace Talos.Core.Models
{

    public enum TracingFilterMode
    {
        /// <summary>
        /// Trace all methods by default
        /// </summary>
        TraceAll,

        /// <summary>
        /// Trace only methods that are explicitly included
        /// </summary>
        OptIn,

        /// <summary>
        /// Trace all methods except those that are explicitly excluded
        /// </summary>
        OptOut
    }

    public class TracingInterceptor(ITracer tracer,
        HashSet<string>? methodFilter = null,
        TracingFilterMode filterMode = TracingFilterMode.TraceAll
        ) : IInterceptor

    {

        public void Intercept(IInvocation invocation)
        {
            // Check if this method should be traced
            if (!ShouldTraceMethod(invocation.Method))
            {
                // Skip tracing and just proceed with the original method
                invocation.Proceed();
                return;
            }

            // Check if return type is Task or Task<T>
            Type returnType = invocation.Method.ReturnType;
            bool isAsync = typeof(Task).IsAssignableFrom(returnType);

            if (isAsync)
                HandleAsyncInvocation(invocation);
            else
            {
                using var span = tracer.StartSpan(invocation.Method.Name, SpanKind.Client);
                invocation.Proceed();
            }
        }

        private bool ShouldTraceMethod(MethodInfo method)
        {
            string methodName = method.Name;

            return filterMode switch
            {
                TracingFilterMode.TraceAll => true,
                TracingFilterMode.OptIn => methodFilter?.Contains(methodName) ?? false,
                TracingFilterMode.OptOut => !methodFilter?.Contains(methodName) ?? true,
                _ => true // Default to tracing everything
            };
        }

        private void HandleAsyncInvocation(IInvocation invocation)
        {
            var span = tracer.StartSpan(invocation.Method.Name, SpanKind.Client);
            invocation.Proceed();

            if (invocation.ReturnValue is not Task originalTask)
            {
                span.Dispose();
                return;
            }

            // Check if it's Task<T> or just Task
            Type returnType = invocation.Method.ReturnType;

            if (returnType == typeof(Task))
                // Simple Task - wrap it in a continuation that disposes the span
                invocation.ReturnValue = WrapTaskWithSpan(originalTask, span);
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Task<T> - need to preserve the result
                Type resultType = returnType.GetGenericArguments()[0];

                // Use reflection to call the generic WrapTaskWithSpan<T> method
                MethodInfo wrapMethod = GetType().GetMethod(nameof(WrapTaskWithSpanGeneric),
                    BindingFlags.NonPublic | BindingFlags.Instance)!;

                MethodInfo genericWrapMethod = wrapMethod.MakeGenericMethod(resultType);
                invocation.ReturnValue = genericWrapMethod.Invoke(this, [originalTask, span]);
            }
        }

        private async Task WrapTaskWithSpan(Task originalTask, IDisposable span)
        {
            try
            {
                await originalTask.ConfigureAwait(false);
            }
            finally
            {
                span.Dispose();
            }
        }

        private async Task<T> WrapTaskWithSpanGeneric<T>(Task originalTask, IDisposable span)
        {
            try
            {
                T result = await ((Task<T>)originalTask).ConfigureAwait(false);
                return result;
            }
            finally
            {
                span.Dispose();
            }
        }
    }
}
