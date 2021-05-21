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

    internal static class ParameterLog
    {
        private static readonly Action<ILogger, string, string, string, Exception?> _parameterBindingFailed = LoggerMessage.Define<string, string, string>(
            LogLevel.Debug,
            new EventId(3, "ParamaterBindingFailed"),
            @"Failed to bind parameter ""{ParameterType} {ParameterName}"" from ""{SourceValue}"".");

        public static void ParameterBindingFailed<T>(HttpContext httpContext, ParameterBinder<T> binder)
        {
            _parameterBindingFailed(GetLogger(httpContext), typeof(T).Name, binder.Name, "", null);
        }

        public static void ParameterBindingFailed<T>(HttpContext httpContext, string name)
        {
            _parameterBindingFailed(GetLogger(httpContext), typeof(T).Name, name, "", null);
        }

        private static ILogger GetLogger(HttpContext httpContext)
        {
            var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            return loggerFactory.CreateLogger(typeof(RequestDelegateFactory));
        }
    }
}
