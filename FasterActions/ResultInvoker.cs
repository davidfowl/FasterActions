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
            if (typeof(T) == typeof(IResult))
            {
                return IResultInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(Task<string>))
            {
                return TaskOfStringInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(Task<IResult>))
            {
                return TaskOfIResultInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(ValueTask<string>))
            {
                return ValueTaskOfStringInvoker<T>.Instance;
            }
            else if (typeof(T) == typeof(ValueTask<IResult>))
            {
                return ValueTaskOfIResultInvoker<T>.Instance;
            }
            else if (typeof(T).IsGenericType)
            {
                if (typeof(T).GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return TaskOfTInvokerCache<T>.Instance.Invoker;
                }
                else if (typeof(T).GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    return ValueTaskOfTInvokerCache<T>.Instance.Invoker;
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

    // TTask = Task<T>
    sealed class TaskOfTInvokerCache<TTask>
    {
        public static readonly ValueTaskOfTInvokerCache<TTask> Instance = new();

        public TaskOfTInvokerCache()
        {
            // Task<T>
            // We need to use MakeGenericType to resolve the T in Task<T>. This is still an issue for AOT support
            // because it won't see the instantiation of the TaskOfTInvoker. 

            var resultType = typeof(TTask).GetGenericArguments()[0];

            Type type;

            // Task<T> where T : IResult
            if (resultType.IsAssignableTo(typeof(IResult)))
            {
                type = typeof(TaskOfTDerivedIResultInvoker<,>).MakeGenericType(typeof(TTask), resultType);
            }
            else
            {
                type = typeof(TaskOfTInvoker<,>).MakeGenericType(typeof(TTask), resultType);
            }

            Invoker = (ResultInvoker<TTask>)Activator.CreateInstance(type)!;
        }

        public ResultInvoker<TTask> Invoker { get; }
    }

    sealed class TaskOfTInvoker<T, TaskResult> : ResultInvoker<T>
    {
        public static readonly TaskOfTInvoker<T, TaskResult> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            await httpContext.Response.WriteAsJsonAsync(await (Task<TaskResult>)(object)result);
        }
    }

    sealed class TaskOfTDerivedIResultInvoker<T, TaskResult> : ResultInvoker<T> where TaskResult : IResult
    {
        public static readonly TaskOfTDerivedIResultInvoker<T, TaskResult> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            await (await (Task<TaskResult>)(object)result).ExecuteAsync(httpContext);
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

    // TTask = ValueTask<T>
    sealed class ValueTaskOfTInvokerCache<TTask>
    {
        public static readonly ValueTaskOfTInvokerCache<TTask> Instance = new();

        public ValueTaskOfTInvokerCache()
        {
            // ValueTask<T>
            // We need to use MakeGenericType to resolve the T in Task<T>. This is still an issue for AOT support
            // because it won't see the instantiation of the TaskOfTInvoker.
            var resultType = typeof(TTask).GetGenericArguments()[0];

            Type type;

            // ValueTask<T> where T : IResult
            if (resultType.IsAssignableTo(typeof(IResult)))
            {
                type = typeof(ValueTaskOfTDerivedIResultInvoker<,>).MakeGenericType(typeof(TTask), resultType);
            }
            else
            {
                type = typeof(ValueTaskOfTInvoker<,>).MakeGenericType(typeof(TTask), resultType);
            }

            Invoker = (ResultInvoker<TTask>)Activator.CreateInstance(type)!;
        }

        public ResultInvoker<TTask> Invoker { get; }
    }

    sealed class ValueTaskOfTInvoker<T, TaskResult> : ResultInvoker<T>
    {
        public static readonly ValueTaskOfTInvoker<T, TaskResult> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await httpContext.Response.WriteAsJsonAsync(await (ValueTask<TaskResult>)(object)result!);
        }
    }

    sealed class ValueTaskOfTDerivedIResultInvoker<T, TaskResult> : ResultInvoker<T> where TaskResult : IResult
    {
        public static readonly ValueTaskOfTDerivedIResultInvoker<T, TaskResult> Instance = new();

        public override async Task Invoke(HttpContext httpContext, T? result)
        {
            await (await (ValueTask<TaskResult>)(object)result!).ExecuteAsync(httpContext);
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
