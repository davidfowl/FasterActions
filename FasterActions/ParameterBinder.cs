using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    public abstract class ParameterBinder<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]T>
    {
        // Try parse methdods that may be defined on T
        private delegate bool TryParse(string s, out T value);

        public abstract bool IsBody { get; }
        public abstract string Name { get; }

        public abstract bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value);
        public abstract ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext);

        private static readonly TryParse? _tryParse = FindTryParseMethod();

        // This needs to be inlinable in order for the JIT to see the newobj call in order
        // to enable devirtualization the method might currently be too big for this...
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParameterBinder<T> Create(ParameterInfo parameterInfo, IServiceProvider serviceProvider)
        {
            var parameterCustomAttributes = Attribute.GetCustomAttributes(parameterInfo);

            // No attributes fast path
            if (parameterCustomAttributes.Length == 0)
            {
                return GetParameterBinderBaseOnType(parameterInfo, serviceProvider);
            }

            return GetBinderBaseOnAttributes(parameterInfo, parameterCustomAttributes, serviceProvider);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ParameterBinder<T> GetBinderBaseOnAttributes(ParameterInfo parameterInfo, Attribute[] parameterCustomAttributes, IServiceProvider serviceProvider)
        {
            if (parameterCustomAttributes.OfType<IFromRouteMetadata>().FirstOrDefault() is { } routeAttribute)
            {
                return new RouteParameterBinder<T>(routeAttribute.Name ?? parameterInfo.Name!);
            }
            else if (parameterCustomAttributes.OfType<IFromQueryMetadata>().FirstOrDefault() is { } queryAttribute)
            {
                return new QueryParameterBinder<T>(queryAttribute.Name ?? parameterInfo.Name!);
            }
            else if (parameterCustomAttributes.OfType<IFromHeaderMetadata>().FirstOrDefault() is { } headerAttribute)
            {
                return new HeaderParameterBinder<T>(headerAttribute.Name ?? parameterInfo.Name!);
            }
            else if (parameterCustomAttributes.OfType<IFromBodyMetadata>().FirstOrDefault() is { } bodyAttribute)
            {
                return new BodyParameterBinder<T>(parameterInfo.Name!, bodyAttribute.AllowEmpty);
            }
            else if (parameterCustomAttributes.Any(a => a is IFromServiceMetadata))
            {
                return new ServicesParameterBinder<T>(parameterInfo.Name!);
            }

            return GetParameterBinderBaseOnType(parameterInfo, serviceProvider);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ParameterBinder<T> GetParameterBinderBaseOnType(ParameterInfo parameterInfo, IServiceProvider serviceProvider)
        {
            if (typeof(T) == typeof(string) ||
                typeof(T) == typeof(byte) ||
                typeof(T) == typeof(short) ||
                typeof(T) == typeof(int) ||
                typeof(T) == typeof(long) ||
                typeof(T) == typeof(decimal) ||
                typeof(T) == typeof(double) ||
                typeof(T) == typeof(float) ||
                typeof(T) == typeof(Guid) ||
                typeof(T) == typeof(DateTime) ||
                typeof(T) == typeof(DateTimeOffset))
            {
                return new RouteOrQueryParameterBinder<T>(parameterInfo.Name!);
            }
            else if (typeof(T) == typeof(HttpContext))
            {
                return new HttpContextParameterBinder<T>(parameterInfo.Name!);
            }
            else if (typeof(T) == typeof(CancellationToken))
            {
                return new CancellationTokenParameterBinder<T>(parameterInfo.Name!);
            }
            else if (typeof(T).IsEnum || _tryParse != null) // Slow fallback for unknown types
            {
                return new RouteOrQueryParameterBinder<T>(parameterInfo.Name!);
            }
            else if (serviceProvider.GetService<IServiceProviderIsService>() is IServiceProviderIsService serviceProviderIsService && serviceProviderIsService.IsService(typeof(T)))
            {
                return new ServicesParameterBinder<T>(parameterInfo.Name!);
            }

            return new BodyParameterBinder<T>(parameterInfo.Name!, allowEmpty: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseValue(string rawValue, [MaybeNullWhen(false)] out T value)
        {
            if (typeof(T) == typeof(string))
            {
                value = (T)(object)rawValue;
                return true;
            }

            if (typeof(T) == typeof(byte))
            {
                bool result = byte.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(byte?))
            {
                if (byte.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(short))
            {
                bool result = short.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(short?))
            {
                if (short.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(int))
            {
                bool result = int.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(int?))
            {
                if (int.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(long))
            {
                bool result = long.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(long?))
            {
                if (long.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(double))
            {
                bool result = double.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(double?))
            {
                if (double.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(float))
            {
                bool result = float.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(float?))
            {
                if (float.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(decimal))
            {
                bool result = decimal.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(decimal?))
            {
                if (decimal.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(Guid))
            {
                bool result = Guid.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(Guid?))
            {
                if (Guid.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(DateTime))
            {
                bool result = DateTime.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(DateTime?))
            {
                if (DateTime.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T) == typeof(DateTimeOffset))
            {
                bool result = DateTimeOffset.TryParse(rawValue, out var parsedValue);
                value = (T)(object)parsedValue;
                return result;
            }

            if (typeof(T) == typeof(DateTimeOffset?))
            {
                if (DateTimeOffset.TryParse(rawValue, out var parsedValue))
                {
                    value = (T)(object)parsedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (typeof(T).IsEnum)
            {
                // This fails because we don't have the the right generic constraints for T
                // return Enum.TryParse<T>(rawValue, out value);

                // This unforunately does boxing :(
                if (Enum.TryParse(typeof(T), rawValue, out var result))
                {
                    value = (T?)result;
                    return value != null;
                }

                value = default;
                return false;
            }

            if (_tryParse == null)
            {
                value = default;
                return false;
            }

            return _tryParse(rawValue, out value);
        }

        private static TryParse? FindTryParseMethod()
        {
            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            var methodInfo = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string), type.MakeByRefType() });

            if (methodInfo != null)
            {
                return methodInfo.CreateDelegate<TryParse>();
            }

            return null;
        }
    }

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
            var rawValue = httpContext.Request.RouteValues[Name]?.ToString() ?? "";

            return TryParseValue(rawValue, out value);
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
            return TryBindValue(httpContext, Name, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBindValue(HttpContext httpContext, string name, [MaybeNullWhen(false)] out T value)
        {
            var rawValue = httpContext.Request.RouteValues[name]?.ToString() ?? httpContext.Request.Query[name].ToString();

            return TryParseValue(rawValue, out value);
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
            var rawValue = httpContext.Request.Query[Name].ToString();

            return TryParseValue(rawValue, out value);
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
            var rawValue = httpContext.Request.Query[Name].ToString();

            return TryParseValue(rawValue, out value);
        }
    }

    sealed class ServicesParameterBinder<T> : ParameterBinder<T>
    {
        public ServicesParameterBinder(string name)
        {
            Name = name;
        }

        public override bool IsBody => false;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
            return new((httpContext.RequestServices.GetRequiredService<T>(), true));
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        }

        public override bool TryBindValue(HttpContext httpContext, [MaybeNullWhen(false)] out T value)
        {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
            value = httpContext.RequestServices.GetRequiredService<T>();
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBindValue(HttpContext httpContext, string name, [MaybeNullWhen(false)] out T value)
        {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
            value = httpContext.RequestServices.GetRequiredService<T>();
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBindValue(HttpContext httpContext, string name, [MaybeNullWhen(false)] out T value)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBindValue(HttpContext httpContext, string name, [MaybeNullWhen(false)] out T value)
        {
            value = (T)(object)httpContext.RequestAborted;
            return true;
        }
    }

    sealed class BodyParameterBinder<T> : ParameterBinder<T>
    {
        private readonly bool _allowEmpty;

        public BodyParameterBinder(string name, bool allowEmpty)
        {
            Name = name;
            _allowEmpty = allowEmpty;
        }

        public override bool IsBody => true;

        public override string Name { get; }

        public override ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext)
        {
            if (_allowEmpty && httpContext.Request.ContentLength == 0)
            {
                return new((default, true));
            }

            return BindBodyOrValueAsync(httpContext, default);
        }

        public static async ValueTask<(T?, bool)> BindBodyOrValueAsync(HttpContext httpContext, string? name)
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

            public static void ParameterBindingFailed(HttpContext httpContext, ParameterBinder<T> binder)
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
