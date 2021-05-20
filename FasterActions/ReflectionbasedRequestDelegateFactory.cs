#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Metadata;

namespace Microsoft.AspNetCore.Http
{
    public static partial class ReflectionbasedRequestDelegateFactory
    {
        public static RequestDelegate CreateRequestDelegate<T0>(Action<T0> func)
        {
            return CreateRequestDelegateCore(new ActionRequestDelegateClosure<T0>(func, func.Method.GetParameters()));
        }

        public static RequestDelegate CreateRequestDelegate<T0, R>(Func<T0, R> func)
        {
            var parameters = func.Method.GetParameters();

            RequestDelegateClosure closure = HasBindingAttributes(parameters) ?
                new TypeOnlyFuncDelegateClosure<T0, R>(func, parameters) :
                new FuncRequestDelegateClosure<T0, R>(func, parameters);

            return CreateRequestDelegateCore(closure);
        }

        public static RequestDelegate CreateRequestDelegate<T0, T1, R>(Func<T0, T1, R> func)
        {
            var parameters = func.Method.GetParameters();

            RequestDelegateClosure closure = HasBindingAttributes(parameters) ?
                new TypeOnlyFuncDelegateClosure<T0, T1, R>(func, parameters) :
                new FuncRequestDelegateClosure<T0, T1, R>(func, parameters);

            return CreateRequestDelegateCore(closure);
        }

        private static bool HasBindingAttributes(ParameterInfo[] parameterInfos)
        {
            foreach (var parameterInfo in parameterInfos)
            {
                foreach (var a in Attribute.GetCustomAttributes(parameterInfo))
                {
                    if (a is IFromRouteMetadata or IFromQueryMetadata or IFromHeaderMetadata or IFromServiceMetadata or IFromBodyMetadata)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // This overload isn't linker friendly
        public static RequestDelegate CreateRequestDelegate(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            bool hasAttributes = HasBindingAttributes(parameters);
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            // This won't be needed in real life
            RequestDelegateClosure closure = new DefaultClosure();

            // We will support up to 16 arguments, then we'll give up

            if (parameters.Length > 16) throw new NotSupportedException("More than 16 arguments isn't supported");

            bool hasReturnType = method.ReturnType != typeof(void);

            var methodInvokerTypes = new Type[hasReturnType ? parameters.Length + 1 : parameters.Length];
            parameterTypes.CopyTo(methodInvokerTypes, 0);

            if (hasReturnType)
            {
                methodInvokerTypes[^1] = method.ReturnType;

                if (parameterTypes.Length == 0)
                {
                    var type = typeof(FuncRequestDelegateClosure<>).MakeGenericType(methodInvokerTypes);

                    var @delegate = method.CreateDelegate(typeof(Func<>).MakeGenericType(methodInvokerTypes));

                    closure = (RequestDelegateClosure)Activator.CreateInstance(type, @delegate)!;
                }
                else if (parameterTypes.Length == 1)
                {
                    var type = hasAttributes ? typeof(FuncRequestDelegateClosure<,>).MakeGenericType(methodInvokerTypes) :
                                               typeof(TypeOnlyFuncDelegateClosure<,>).MakeGenericType(methodInvokerTypes);

                    var @delegate = method.CreateDelegate(typeof(Func<,>).MakeGenericType(methodInvokerTypes));

                    closure = (RequestDelegateClosure)Activator.CreateInstance(type, @delegate, parameters)!;
                }
            }
            else
            {
                if (parameterTypes.Length == 1)
                {
                    var type = typeof(ActionRequestDelegateClosure<>).MakeGenericType(methodInvokerTypes);

                    var @delegate = method.CreateDelegate(typeof(Action<,>).MakeGenericType(methodInvokerTypes));

                    closure = (RequestDelegateClosure)Activator.CreateInstance(type, @delegate, parameters)!;
                }
            }

            return CreateRequestDelegateCore(closure);
        }

        public static RequestDelegate CreateRequestDelegate(Delegate @delegate)
        {
            // It's expensive to get the Method from a delegate https://github.com/dotnet/runtime/blob/64303750a9198a49f596bcc3aa13de804e421579/src/coreclr/System.Private.CoreLib/src/System/Delegate.CoreCLR.cs#L164
            var method = @delegate.Method;
            var parameters = method.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            // This won't be needed in real life
            RequestDelegateClosure closure = new DefaultClosure();

            // We will support up to 16 arguments, then we'll give up

            if (parameters.Length > 16) throw new NotSupportedException("More than 16 arguments isn't supported");

            bool hasReturnType = method.ReturnType != typeof(void);

            var methodInvokerTypes = new Type[hasReturnType ? parameters.Length + 1 : parameters.Length];
            parameterTypes.CopyTo(methodInvokerTypes, 0);

            if (hasReturnType)
            {
                methodInvokerTypes[^1] = method.ReturnType;

                if (parameterTypes.Length == 0)
                {
                    var type = typeof(FuncRequestDelegateClosure<>).MakeGenericType(methodInvokerTypes);

                    closure = (RequestDelegateClosure)Activator.CreateInstance(type, @delegate)!;
                }
                else if (parameterTypes.Length == 1)
                {
                    var type = typeof(FuncRequestDelegateClosure<,>).MakeGenericType(methodInvokerTypes);

                    closure = (RequestDelegateClosure)Activator.CreateInstance(type, @delegate, parameters)!;
                }
            }
            else
            {
                if (parameterTypes.Length == 1)
                {
                    var type = typeof(ActionRequestDelegateClosure<>).MakeGenericType(methodInvokerTypes);

                    closure = (RequestDelegateClosure)Activator.CreateInstance(type, @delegate, parameters)!;
                }
            }

            return CreateRequestDelegateCore(closure);
        }

        private static RequestDelegate CreateRequestDelegateCore(RequestDelegateClosure closure)
        {
            if (closure.HasBody)
            {
                return closure.ProcessRequestWithBodyAsync;
            }

            return closure.ProcessRequestAsync;
        }

        class DefaultClosure : RequestDelegateClosure
        {
            public override bool HasBody => false;

            public override Task ProcessRequestAsync(HttpContext httpContext)
            {
                return httpContext.Response.WriteAsync("Hello World");
            }

            public override Task ProcessRequestWithBodyAsync(HttpContext httpContext)
            {
                throw new NotImplementedException();
            }
        }
    }
}
