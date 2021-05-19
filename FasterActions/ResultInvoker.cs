#nullable enable

using System;
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
        public static ResultInvoker<T> Create()
        {
            if (typeof(T) == typeof(void))
            {
                return CompletedTaskResultInvoker<T>.Instance;
            }
            else if (typeof(T).IsAssignableTo(typeof(IResult)))
            {
                return ExecuteIResultInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(string))
            {
                return ExecuteStringInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(Task))
            {
                return ExecuteTaskInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(ValueTask))
            {
                return ExecuteValueTaskInvoker<T>.Instance;
            }

            else if (typeof(T).IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = typeof(T).GetGenericArguments()[0];

                // Task<IResult> 
                if (resultType == typeof(IResult))
                {
                    return ExecuteTaskOfIResultInvoker<T>.Instance;
                }
                // Task<string> 
                else if (resultType == typeof(string))
                {
                    return ExecuteTaskOfStringInvoker<T>.Instance;
                }
                else
                {
                    // Task<T>
                    // We need to use MakeGeneric method to get the correct version of the method to call
                    // this is still a gap with safe native AOT support.

                    var type = typeof(ExecuteGenericTaskInvoker<,>).MakeGenericType(typeof(T), resultType);
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
                    return ExecuteValueTaskOfIResultInvoker<T>.Instance;
                }
                // ValueTask<string> 
                else if (resultType == typeof(string))
                {
                    return ExecuteValueTaskOfStringInvoker<T>.Instance;
                }
                else
                {
                    // ValueTask<T>
                    // We need to use MakeGeneric method to get the correct version of the method to call
                    // this is still a gap with safe native AOT support.

                    var type = typeof(ExecuteGenericValueTaskInvoker<,>).MakeGenericType(typeof(T), resultType);
                    return (ResultInvoker<T>)Activator.CreateInstance(type)!;
                }
            }

            return ExecuteDefaultInvoker<T>.Instance;
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

    sealed class ExecuteIResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteIResultInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return ((IResult)(object)result!).ExecuteAsync(httpContext);
        }
    }

    sealed class ExecuteStringInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteStringInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return httpContext.Response.WriteAsync((string)(object)result!);
        }
    }

    sealed class ExecuteTaskInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteTaskInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return (Task)(object)result;
        }
    }

    sealed class ExecuteValueTaskInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteValueTaskInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return ((ValueTask)(object)result).AsTask();
        }
    }

    sealed class ExecuteTaskOfIResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteTaskOfIResultInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await (await (Task<IResult>)(object)result!).ExecuteAsync(httpContext);
        }
    }

    sealed class ExecuteTaskOfStringInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteTaskOfStringInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await httpContext.Response.WriteAsync(await (Task<string>)(object)result!);
        }
    }

    sealed class ExecuteGenericTaskInvoker<T, TaskResult> : ResultInvoker<T>
    {
        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await httpContext.Response.WriteAsJsonAsync(await (Task<TaskResult>)(object)result!);
        }
    }

    sealed class ExecuteValueTaskOfIResultInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteValueTaskOfIResultInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await (await (ValueTask<IResult>)(object)result!).ExecuteAsync(httpContext);
        }
    }

    sealed class ExecuteValueTaskOfStringInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteValueTaskOfStringInvoker<T> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await httpContext.Response.WriteAsync(await (ValueTask<string>)(object)result!);
        }
    }

    sealed class ExecuteGenericValueTaskInvoker<T, TaskResult> : ResultInvoker<T>
    {
        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await httpContext.Response.WriteAsJsonAsync(await (ValueTask<TaskResult>)(object)result!);
        }
    }

    sealed class ExecuteDefaultInvoker<T> : ResultInvoker<T>
    {
        public static readonly ExecuteDefaultInvoker<T> Instance = new();

        public override Task Invoke(HttpContext httpContext, T? result)
        {
            return httpContext.Response.WriteAsJsonAsync(result);
        }
    }
}
