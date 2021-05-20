#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// This type captures the state required to execute the request. Processing requests with and without the body
    /// are separated as an optimization step.
    /// </summary>
    public abstract class RequestDelegateClosure
    {
        public abstract bool HasBody { get; }

        public abstract Task ProcessRequestAsync(HttpContext httpContext);
        public abstract Task ProcessRequestWithBodyAsync(HttpContext httpContext);
    }

    // These implementations would be code generated

    sealed class ActionRequestDelegateClosure<T0> : RequestDelegateClosure
    {
        private readonly ParameterBinder<T0> _parameterBinder;
        private readonly Action<T0> _delegate;

        public override bool HasBody => _parameterBinder.IsBody;

        public ActionRequestDelegateClosure(Action<T0> @delegate, ParameterInfo[] parameters)
        {
            _parameterBinder = ParameterBinder<T0>.Create(parameters[0]);
            _delegate = @delegate;
        }

        public override Task ProcessRequestAsync(HttpContext httpContext)
        {
            T0? arg0;

            // Ideally this call would be inlined by the JIT but that would require dynamic PGO
            if (!_parameterBinder.TryBindValue(httpContext, out arg0))
            {
                ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder);
                httpContext.Response.StatusCode = 400;
                return Task.CompletedTask;
            }

            _delegate(arg0);

            return Task.CompletedTask;
        }

        public override async Task ProcessRequestWithBodyAsync(HttpContext httpContext)
        {
            // We know this is a body parameter and it'll throw if it fails to bind
            (T0? arg0, _) = await _parameterBinder.BindBodyOrValueAsync(httpContext);

            _delegate(arg0!);
        }
    }

    sealed class FuncRequestDelegateClosure<R> : RequestDelegateClosure
    {
        private readonly ResultInvoker<R> _resultInvoker;
        private readonly Func<R> _delegate;

        public override bool HasBody => false;

        public FuncRequestDelegateClosure(Func<R> @delegate)
        {
            _delegate = @delegate;
            _resultInvoker = ResultInvoker<R>.Create();
        }

        public override Task ProcessRequestAsync(HttpContext httpContext)
        {
            R? result = _delegate();

            return _resultInvoker.Invoke(httpContext, result!);
        }

        public override Task ProcessRequestWithBodyAsync(HttpContext httpContext)
        {
            // No, arguments, means no body
            throw new NotSupportedException();
        }
    }

    sealed class FuncRequestDelegateClosure<T0, R> : RequestDelegateClosure
    {
        private readonly ParameterBinder<T0> _parameterBinder;
        private readonly ResultInvoker<R> _resultInvoker;
        private readonly Func<T0, R> _delegate;

        public override bool HasBody => _parameterBinder.IsBody;

        public FuncRequestDelegateClosure(Func<T0, R> @delegate, ParameterInfo[] parameters)
        {
            _parameterBinder = ParameterBinder<T0>.Create(parameters[0]);
            _delegate = @delegate;
            _resultInvoker = ResultInvoker<R>.Create();
        }

        public override Task ProcessRequestAsync(HttpContext httpContext)
        {
            T0? arg0;

            // Ideally this call would be inlined by the JIT but that would require dynamic PGO
            if (!_parameterBinder.TryBindValue(httpContext, out arg0))
            {
                ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder);
                httpContext.Response.StatusCode = 400;
                return Task.CompletedTask;
            }

            R? result = _delegate(arg0!);

            return _resultInvoker.Invoke(httpContext, result!);
        }

        public override async Task ProcessRequestWithBodyAsync(HttpContext httpContext)
        {
            (T0? arg0, bool success) = await _parameterBinder.BindBodyOrValueAsync(httpContext);

            if (!success)
            {
                ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder);
                httpContext.Response.StatusCode = 400;
                return;
            }

            R? result = _delegate(arg0!);

            await _resultInvoker.Invoke(httpContext, result!);
        }
    }

    sealed class FuncRequestDelegateClosure<T0, T1, R> : RequestDelegateClosure
    {
        private readonly ParameterBinder<T0> _parameterBinder0;
        private readonly ParameterBinder<T1> _parameterBinder1;
        private readonly ResultInvoker<R> _resultInvoker;
        private readonly Func<T0, T1, R> _delegate;

        public FuncRequestDelegateClosure(Func<T0, T1, R> @delegate, ParameterInfo[] parameters)
        {
            _parameterBinder0 = ParameterBinder<T0>.Create(parameters[0]);
            _parameterBinder1 = ParameterBinder<T1>.Create(parameters[1]);
            _delegate = @delegate;
            _resultInvoker = ResultInvoker<R>.Create();
        }

        public override bool HasBody => _parameterBinder0.IsBody || _parameterBinder1.IsBody;

        public override Task ProcessRequestAsync(HttpContext httpContext)
        {
            T0? arg0;
            T1? arg1;

            if (!_parameterBinder0.TryBindValue(httpContext, out arg0))
            {
                ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder0);
                httpContext.Response.StatusCode = 400;
                return Task.CompletedTask;
            }

            if (!_parameterBinder1.TryBindValue(httpContext, out arg1))
            {
                ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder1);
                httpContext.Response.StatusCode = 400;
                return Task.CompletedTask;
            }

            R? result = _delegate(arg0, arg1);

            return _resultInvoker.Invoke(httpContext, result!);
        }

        public override async Task ProcessRequestWithBodyAsync(HttpContext httpContext)
        {
            bool success;
            (T0? arg0, success) = await _parameterBinder0.BindBodyOrValueAsync(httpContext);

            if (!success)
            {
                ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder0);
                httpContext.Response.StatusCode = 400;
                return;
            }

            (T1? arg1, success) = await _parameterBinder1.BindBodyOrValueAsync(httpContext);

            if (!success)
            {
                ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder1);
                httpContext.Response.StatusCode = 400;
                return;
            }

            R? result = _delegate(arg0!, arg1!);

            await _resultInvoker.Invoke(httpContext, result!);
        }
    }

    public static class ParameterLog
    {
        private static readonly Action<ILogger, string, string, string, Exception?> _parameterBindingFailed = LoggerMessage.Define<string, string, string>(
            LogLevel.Debug,
            new EventId(3, "ParamaterBindingFailed"),
            @"Failed to bind parameter ""{ParameterType} {ParameterName}"" from ""{SourceValue}"".");

        public static void ParameterBindingFailed<T>(HttpContext httpContext, ParameterBinder<T> binder)
        {
            _parameterBindingFailed(GetLogger(httpContext), typeof(T).Name, binder.Name, "", null);
        }

        private static ILogger GetLogger(HttpContext httpContext)
        {
            var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            return loggerFactory.CreateLogger(typeof(RequestDelegateFactory));
        }
    }
}
