﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// <see cref="ParameterBinder"/> represents 2 kinds of parameters:
    /// 1. Ones that are fast and synchronous. This can be something like reading something
    /// that's pre-materialized on the HttpContext (query string, header, route value etc). (invoked via BindValue)
    /// 2. Ones that are asynchronous and potentially IO bound. An example is reading a JSON body
    /// from an http request (or reading a file). (invoked via BindBodyAsync)
    /// </summary>
    public abstract class ParameterBinder<T>
    {
        public abstract bool IsBody { get; }
        public abstract string Name { get; }

        public abstract bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value);
        public abstract ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext);

        private static readonly MethodInfo EnumTryParseMethod = GetEnumTryParseMethod();
        private static readonly ConcurrentDictionary<Type, MethodInfo?> TryParseMethodCache = new();

        // This needs to be inlinable in order for the JIT to see the newobj call in order
        // to enable devirtualization the method might currently be too big for this...
        public static ParameterBinder<T> Create(ParameterInfo parameterInfo)
        {
            if (parameterInfo.Name is null)
            {
                throw new NotSupportedException("Parameter must have a name");
            }

            var parameterCustomAttributes = parameterInfo.GetCustomAttributes();

            // TODO: This is missing support for calling a custom TryParse methods

            if (parameterCustomAttributes.OfType<IFromRouteMetadata>().FirstOrDefault() is { } routeAttribute)
            {
                return new RouteParameterBinder<T>(routeAttribute.Name ?? parameterInfo.Name);
            }
            else if (parameterCustomAttributes.OfType<IFromQueryMetadata>().FirstOrDefault() is { } queryAttribute)
            {
                return new QueryParameterBinder<T>(queryAttribute.Name ?? parameterInfo.Name);
            }
            else if (parameterCustomAttributes.OfType<IFromHeaderMetadata>().FirstOrDefault() is { } headerAttribute)
            {
                return new HeaderParameterBinder<T>(headerAttribute.Name ?? parameterInfo.Name);
            }
            else if (parameterCustomAttributes.OfType<IFromBodyMetadata>().FirstOrDefault() is { } bodyAttribute)
            {
                return new BodyParameterBinder<T>(parameterInfo.Name, bodyAttribute.AllowEmpty);
            }
            else if (parameterInfo.CustomAttributes.Any(a => typeof(IFromServiceMetadata).IsAssignableFrom(a.AttributeType)))
            {
                return new ServicesParameterBinder<T>(parameterInfo.Name);
            }
            else if (parameterInfo.ParameterType == typeof(HttpContext))
            {
                return new HttpContextParameterBinder<T>(parameterInfo.Name);
            }
            else if (parameterInfo.ParameterType == typeof(CancellationToken))
            {
                return new CancellationTokenParameterBinder<T>(parameterInfo.Name);
            }
            else if (parameterInfo.ParameterType == typeof(string) || parameterInfo.ParameterType.IsPrimitive || HasTryParseMethod(parameterInfo))
            {
                return new RouteOrQueryParameterBinder<T>(parameterInfo.Name);
            }
            else if (parameterInfo.ParameterType.IsInterface)
            {
                return new ServicesParameterBinder<T>(parameterInfo.Name);
            }

            return new BodyParameterBinder<T>(parameterInfo.Name, allowEmpty: false);
        }


        private static MethodInfo GetEnumTryParseMethod()
        {
            var staticEnumMethods = typeof(Enum).GetMethods(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in staticEnumMethods)
            {
                if (!method.IsGenericMethod || method.Name != "TryParse" || method.ReturnType != typeof(bool))
                {
                    continue;
                }

                var tryParseParameters = method.GetParameters();

                if (tryParseParameters.Length == 2 &&
                    tryParseParameters[0].ParameterType == typeof(string) &&
                    tryParseParameters[1].IsOut)
                {
                    return method;
                }
            }

            throw new Exception("static bool System.Enum.TryParse<TEnum>(string? value, out TEnum result) does not exist!!?!?");
        }

        // TODO: Use InvariantCulture where possible? Or is CurrentCulture fine because it's more flexible?
        private static MethodInfo? FindTryParseMethod(Type type)
        {
            static MethodInfo? Finder(Type type)
            {
                if (type.IsEnum)
                {
                    return EnumTryParseMethod.MakeGenericMethod(type);
                }

                var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

                foreach (var method in staticMethods)
                {
                    if (method.Name != "TryParse" || method.ReturnType != typeof(bool))
                    {
                        continue;
                    }

                    var tryParseParameters = method.GetParameters();

                    if (tryParseParameters.Length == 2 &&
                        tryParseParameters[0].ParameterType == typeof(string) &&
                        tryParseParameters[1].IsOut &&
                        tryParseParameters[1].ParameterType == type.MakeByRefType())
                    {
                        return method;
                    }
                }

                return null;
            }

            return TryParseMethodCache.GetOrAdd(type, Finder);
        }

        private static bool HasTryParseMethod(ParameterInfo parameter)
        {
            var nonNullableParameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            return FindTryParseMethod(nonNullableParameterType) is not null;
        }
    }

    // REVIEW: These are still boxing by using Convert.ChangeType, but we could code generate implementations
    // for each combination of source and known type that supports TryParse

    sealed class RouteParameterBinder<T> : ParameterBinder<T>
    {
        public RouteParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            bool success = TryBindValue(httpContext, out var value);

            return new((value, success));
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            value = (T)Convert.ChangeType(httpContext.Request.RouteValues[Name]?.ToString(), typeof(T));
            return true;
        }
    }

    sealed class RouteOrQueryParameterBinder<T> : ParameterBinder<T>
    {
        public RouteOrQueryParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            bool success = TryBindValue(httpContext, out var value);

            return new((value, success));
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            var rawValue = httpContext.Request.RouteValues[Name]?.ToString() ?? httpContext.Request.Query[Name].ToString();

            value = (T)Convert.ChangeType(rawValue, typeof(T));
            return true;
        }
    }

    sealed class QueryParameterBinder<T> : ParameterBinder<T>
    {
        public QueryParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            bool success = TryBindValue(httpContext, out var value);

            return new((value, success));
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            value = (T)Convert.ChangeType(httpContext.Request.Query[Name].ToString(), typeof(T));
            return true;
        }
    }

    sealed class HeaderParameterBinder<T> : ParameterBinder<T>
    {
        public HeaderParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            bool success = TryBindValue(httpContext, out var value);

            return new((value, success));
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            value = (T)Convert.ChangeType(httpContext.Request.Headers[Name].ToString(), typeof(T));
            return true;
        }
    }

    sealed class ServicesParameterBinder<T> : ParameterBinder<T> where T : notnull
    {
        public ServicesParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            return new((httpContext.RequestServices.GetRequiredService<T>(), true));
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            value = httpContext.RequestServices.GetRequiredService<T>();
            return true;
        }
    }

    sealed class HttpContextParameterBinder<T> : ParameterBinder<T>
    {
        public HttpContextParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            return new(((T)(object)httpContext, true));
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            value = (T)(object)httpContext;
            return true;
        }
    }

    sealed class CancellationTokenParameterBinder<T> : ParameterBinder<T>
    {
        public CancellationTokenParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            return new(((T)(object)httpContext.RequestAborted, true));
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            value = (T)(object)httpContext.RequestAborted;
            return true;
        }
    }

    sealed class BodyParameterBinder<T> : ParameterBinder<T>
    {
        public BodyParameterBinder(string name, bool allowEmpty)
        {
            Name = name;
        }

        public override bool IsBody => true;

        public override string Name { get; }

        public override async ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            try
            {
                return (await httpContext.Request.ReadFromJsonAsync<T>(), true);
            }
            catch (IOException ex)
            {
                Log.RequestBodyIOException(httpContext, ex);
                return (default, false);
            }
            catch (InvalidDataException ex)
            {
                Log.RequestBodyInvalidDataException(httpContext, ex);
                return (default, false);
            }
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
            throw new NotSupportedException("Synchronous value binding isn't supported");
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _requestBodyIOException = LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(1, "RequestBodyIOException"),
                "Reading the request body failed with an IOException.");

            private static readonly Action<ILogger, Exception> _requestBodyInvalidDataException = LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(2, "RequestBodyInvalidDataException"),
                "Reading the request body failed with an InvalidDataException.");

            private static readonly Action<ILogger, string, string, string, Exception?> _parameterBindingFailed = LoggerMessage.Define<string, string, string>(
                LogLevel.Debug,
                new EventId(3, "ParamaterBindingFailed"),
                @"Failed to bind parameter ""{ParameterType} {ParameterName}"" from ""{SourceValue}"".");

            public static void RequestBodyIOException(HttpContext httpContext, IOException exception)
            {
                _requestBodyIOException(GetLogger(httpContext), exception);
            }

            public static void RequestBodyInvalidDataException(HttpContext httpContext, InvalidDataException exception)
            {
                _requestBodyInvalidDataException(GetLogger(httpContext), exception);
            }

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
}
