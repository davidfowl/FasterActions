#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// <see cref="ResultInvoker"/> a wrapper around a function pointer that processes the result.
    /// </summary>
    public abstract class ResultInvoker<T>
    {
        public abstract Task Invoke(HttpContext httpContext, T? result);

        // Ideally this would be inlinable so the JIT can de-virtualize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ResultInvoker<T> Create()
        {
            if (typeof(T) == typeof(void))
            {
                return CompletedTaskResultInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(string))
            {
                return StringResultInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(Task))
            {
                return TaskInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(ValueTask))
            {
                return ValueTaskInvoker<T>.Instance;
            }
            else if (typeof(T).IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = typeof(T).GetGenericArguments()[0];

                // Task<IResult> 
                if (resultType == typeof(IResult))
                {
                    return TaskOfIResultInvoker<T>.Instance;
                }
                // Task<string> 
                else if (resultType == typeof(string))
                {
                    return TaskOfStringInvoker<T>.Instance;
                }
                else
                {
                    // Task<T>
                    // We need to use MakeGeneric method to get the correct version of the method to call
                    // this is still a gap with safe native AOT support.

                    var type = typeof(TaskOfTInvoker<,>).MakeGenericType(typeof(T), resultType);
                    return (ResultInvoker<T>)Activator.CreateInstance(type)!;
                }
            }
            else if (typeof(T).IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var resultType = typeof(T).GetGenericArguments()[0];

                // ValueTask<IResult> 
                if (resultType == typeof(IResult))
                {
                    return ValueTaskOfIResultInvoker<T>.Instance;
                }
                // ValueTask<string> 
                else if (resultType == typeof(string))
                {
                    return ValueTaskOfStringInvoker<T>.Instance;
                }
                else
                {
                    // ValueTask<T>
                    // We need to use MakeGeneric method to get the correct version of the method to call
                    // this is still a gap with safe native AOT support.

                    var type = typeof(ValueTaskOfTInvoker<,>).MakeGenericType(typeof(T), resultType);
                    return (ResultInvoker<T>)Activator.CreateInstance(type)!;
                }
            }
            else if (typeof(T).IsAssignableTo(typeof(IResult)))
            {
                return IResultInvoker<T>.Instance;
            }

            return DefaultInvoker<T>.Instance;
        }
    }

    sealed class CompletedTaskResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly CompletedTaskResultInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return Task.CompletedTask;
        }
    }

    sealed class IResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly IResultInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return ((IResult)(object)result!).ExecuteAsync(httpContext);
        }
    }

    sealed class StringResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly StringResultInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return httpContext.Response.WriteAsync((string)(object)result!);
        }
    }

    sealed class TaskInvoker<T> : ResultInvoker<T>
    {
        public static readonly TaskInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            return (Task)(object)result;
        }
    }

    sealed class ValueTaskInvoker<T> : ResultInvoker<T>
    {
        public static readonly ValueTaskInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return ((ValueTask)(object)result!).AsTask();
        }
    }

    sealed class TaskOfIResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly TaskOfIResultInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            await (await (Task<IResult>)(object)result).ExecuteAsync(httpContext);
        }
    }

    sealed class TaskOfStringInvoker<T> : ResultInvoker<T>
    {
        public static readonly TaskOfStringInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            await httpContext.Response.WriteAsync(await (Task<string>)(object)result);
        }
    }

    sealed class TaskOfTInvoker<T, TaskResult> : ResultInvoker<T>
    {
        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            await httpContext.Response.WriteAsJsonAsync(await (Task<TaskResult>)(object)result);
        }
    }

    sealed class ValueTaskOfIResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly ValueTaskOfIResultInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await (await (ValueTask<IResult>)(object)result!).ExecuteAsync(httpContext);
        }
    }

    sealed class ValueTaskOfStringInvoker<T> : ResultInvoker<T>
    {
        public static readonly ValueTaskOfStringInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await httpContext.Response.WriteAsync(await (ValueTask<string>)(object)result!);
        }
    }

    sealed class ValueTaskOfTInvoker<T, TaskResult> : ResultInvoker<T>
    {
        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await httpContext.Response.WriteAsJsonAsync(await (ValueTask<TaskResult>)(object)result!);
        }
    }

    sealed class DefaultInvoker<T> : ResultInvoker<T>
    {
        public static readonly DefaultInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return httpContext.Response.WriteAsJsonAsync(result);
        }
    }
}
