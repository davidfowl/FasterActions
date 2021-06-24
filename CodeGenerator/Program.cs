﻿using System;
using System.Text;

namespace CodeGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(new ClosureGenerator().Generate());
        }
    }

    class ClosureGenerator
    {
        private readonly StringBuilder _codeBuilder = new StringBuilder();
        private int _indent;
        private int _column;

        public void Indent()
        {
            _indent++;
        }

        public void Unindent()
        {
            _indent--;
        }

        public string Generate()
        {
            WriteLine("#nullable enable");
            WriteLine("#pragma warning disable CS1998");
            Write($@"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:{Environment.Version}
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------");
            WriteLine("");
            WriteLine("");

            WriteLine("namespace Microsoft.AspNetCore.Http");
            WriteLine("{");
            Indent();

            for (int arity = 0; arity <= 16; arity++)
            {
                GenerateDelegateClosure(arity, hasReturnType: false);
                GenerateDelegateClosure(arity, hasReturnType: true);
            }

            Unindent();
            WriteLine("}"); // namespace

            return _codeBuilder.ToString();
        }

        private void GenerateTypeOnlyDelegateClosure(int arity, bool hasReturnType = true)
        {
            var typeName = hasReturnType ? "TypeOnlyFuncRequestDelegateClosure" : "TypeOnlyActionRequestDelegateClosure";

            Write($"sealed class {typeName}");
            WriteGenericParameters(arity, hasReturnType);

            Write(" : Microsoft.AspNetCore.Http.RequestDelegateClosure");
            WriteLine();
            WriteLine("{");
            Indent();
            Write("public override bool HasBody => ");
            for (int j = 0; j < arity; j++)
            {
                if (j > 0)
                {
                    Write(" || ");
                }
                Write($"Microsoft.AspNetCore.Http.ParameterBinder<T{j}>.HasBodyBasedOnType");
            }
            if (arity == 0)
            {
                Write("false");
            }
            Write(";");
            WriteLine();
            WriteLine();
            for (int j = 0; j < arity; j++)
            {
                WriteLine($"private readonly string _name{j};");
            }
            Write("private readonly ");
            WriteFuncOrActionType(arity, hasReturnType);

            WriteLine(" _delegate;");
            WriteLine();
            Write($"public {typeName}(");
            WriteFuncOrActionType(arity, hasReturnType);

            WriteLine(" @delegate, System.Reflection.ParameterInfo[] parameters)");
            WriteLine("{");
            Indent();
            WriteLine("_delegate = @delegate;");
            for (int j = 0; j < arity; j++)
            {
                WriteLine($"_name{j} = parameters[{j}].Name!;");
            }
            Unindent();
            WriteLine("}"); //ctor

            WriteLine();
            WriteLine("public override System.Threading.Tasks.Task ProcessRequestAsync(Microsoft.AspNetCore.Http.HttpContext httpContext)");
            WriteLine("{");
            Indent();
            for (int j = 0; j < arity; j++)
            {
                WriteLine($"if (!Microsoft.AspNetCore.Http.ParameterBinder<T{j}>.TryBindValueBasedOnType(httpContext, _name{j}, out var arg{j}))");
                WriteLine("{");
                Indent();
                WriteLine($"Microsoft.AspNetCore.Http.ParameterLog.ParameterBindingFailed<T{j}>(httpContext, _name{j});");
                WriteLine("httpContext.Response.StatusCode = 400;");
                WriteLine("return System.Threading.Tasks.Task.CompletedTask;");
                Unindent();
                WriteLine("}");
                WriteLine();
            }

            WriteDelegateCall(arity, hasReturnType);

            WriteLine();

            if (hasReturnType)
            {
                WriteLine("return Microsoft.AspNetCore.Http.ResultInvoker<R>.Instance.Invoke(httpContext, result);");
            }
            else
            {
                WriteLine("return System.Threading.Tasks.Task.CompletedTask;");
            }

            Unindent();
            WriteLine("}"); // ProcessRequestAsync

            WriteLine();
            WriteLine("public override async System.Threading.Tasks.Task ProcessRequestWithBodyAsync(Microsoft.AspNetCore.Http.HttpContext httpContext)");
            WriteLine("{");
            Indent();

            if (arity > 0)
            {
                WriteLine("var success = false;");
            }

            for (int j = 0; j < arity; j++)
            {
                WriteLine($"(T{j}? arg{j}, success) = await Microsoft.AspNetCore.Http.ParameterBinder<T{j}>.BindBodyBasedOnType(httpContext, _name{j});");
                WriteLine();
                WriteLine("if (!success)");
                WriteLine("{");
                Indent();
                WriteLine($"Microsoft.AspNetCore.Http.ParameterLog.ParameterBindingFailed<T{j}>(httpContext, _name{j});");
                WriteLine("httpContext.Response.StatusCode = 400;");
                WriteLine("return;");
                Unindent();
                WriteLine("}");
                WriteLine();
            }

            WriteDelegateCall(arity, hasReturnType);

            if (hasReturnType)
            {
                WriteLine();
                WriteLine("await Microsoft.AspNetCore.Http.ResultInvoker<R>.Instance.Invoke(httpContext, result);");
            }

            Unindent();
            WriteLine("}");

            Unindent();
            WriteLine("}");
            WriteLine();
        }

        private void GenerateDelegateClosure(int arity, bool hasReturnType = false)
        {
            var typeName = hasReturnType ? "FuncRequestDelegateClosure" : "ActionRequestDelegateClosure";

            Write($"sealed class {typeName}");
            WriteGenericParameters(arity, hasReturnType);
            Write(" : Microsoft.AspNetCore.Http.RequestDelegateClosure");
            WriteLine();
            WriteLine("{");
            Indent();
            Write("public override bool HasBody => ");
            for (int j = 0; j < arity; j++)
            {
                if (j > 0)
                {
                    Write(" || ");
                }
                Write($"_parameterBinder{j}.IsBody");
            }
            if (arity == 0)
            {
                Write("false");
            }
            Write(";");
            WriteLine();
            WriteLine();
            for (int j = 0; j < arity; j++)
            {
                WriteLine($"private readonly Microsoft.AspNetCore.Http.ParameterBinder<T{j}> _parameterBinder{j};");
            }
            Write("private readonly ");
            WriteFuncOrActionType(arity, hasReturnType);
            WriteLine(" _delegate;");
            WriteLine();
            Write($"public {typeName}(");
            WriteFuncOrActionType(arity, hasReturnType);
            WriteLine(" @delegate, System.Reflection.ParameterInfo[] parameters, System.IServiceProvider serviceProvider)");
            WriteLine("{");
            Indent();
            WriteLine("_delegate = @delegate;");

            for (int j = 0; j < arity; j++)
            {
                WriteLine($"_parameterBinder{j} = Microsoft.AspNetCore.Http.ParameterBinder<T{j}>.Create(parameters[{j}], serviceProvider);");
            }
            Unindent();
            WriteLine("}"); //ctor

            WriteLine();
            WriteLine("public override System.Threading.Tasks.Task ProcessRequestAsync(Microsoft.AspNetCore.Http.HttpContext httpContext)");
            WriteLine("{");
            Indent();
            for (int j = 0; j < arity; j++)
            {
                WriteLine($"if (!_parameterBinder{j}.TryBindValue(httpContext, out var arg{j}))");
                WriteLine("{");
                Indent();
                WriteLine($"Microsoft.AspNetCore.Http.ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder{j});");
                WriteLine("httpContext.Response.StatusCode = 400;");
                WriteLine("return System.Threading.Tasks.Task.CompletedTask;");
                Unindent();
                WriteLine("}");
                WriteLine();
            }

            WriteDelegateCall(arity, hasReturnType);

            WriteLine();

            if (hasReturnType)
            {
                WriteLine("return Microsoft.AspNetCore.Http.ResultInvoker<R>.Instance.Invoke(httpContext, result);");
            }
            else
            {
                WriteLine("return System.Threading.Tasks.Task.CompletedTask;");
            }

            Unindent();
            WriteLine("}"); // ProcessRequestAsync

            WriteLine();
            WriteLine("public override async System.Threading.Tasks.Task ProcessRequestWithBodyAsync(Microsoft.AspNetCore.Http.HttpContext httpContext)");
            WriteLine("{");
            Indent();

            if (arity > 0)
            {
                WriteLine("var success = false;");
            }

            for (int j = 0; j < arity; j++)
            {
                WriteLine($"(T{j}? arg{j}, success) = await _parameterBinder{j}.BindBodyOrValueAsync(httpContext);");
                WriteLine();
                WriteLine("if (!success)");
                WriteLine("{");
                Indent();
                WriteLine($"Microsoft.AspNetCore.Http.ParameterLog.ParameterBindingFailed(httpContext, _parameterBinder{j});");
                WriteLine("httpContext.Response.StatusCode = 400;");
                WriteLine("return;");
                Unindent();
                WriteLine("}");
                WriteLine();
            }

            WriteDelegateCall(arity, hasReturnType);

            if (hasReturnType)
            {
                WriteLine();
                WriteLine("await Microsoft.AspNetCore.Http.ResultInvoker<R>.Instance.Invoke(httpContext, result);");
            }

            Unindent();
            WriteLine("}");

            Unindent();
            WriteLine("}");
            WriteLine();
        }

        private void WriteGenericParameters(int arity, bool hasReturnType)
        {
            if (arity > 0 || hasReturnType)
            {
                Write("<");
            }

            for (int j = 0; j < arity; j++)
            {
                if (j > 0)
                {
                    Write(", ");
                }
                Write($"T{j}");
            }
            if (hasReturnType)
            {
                if (arity == 0)
                {
                    Write("R>");
                }
                else
                {
                    Write(", R>");
                }
            }
            else
            {
                if (arity > 0)
                {
                    Write(">");
                }
            }
        }

        private void WriteDelegateCall(int arity, bool hasReturnType)
        {
            if (hasReturnType)
            {
                Write("R? result = _delegate(");
            }
            else
            {
                Write("_delegate(");
            }
            for (int j = 0; j < arity; j++)
            {
                if (j > 0)
                {
                    Write(", ");
                }
                Write($"arg{j}!");
            }
            WriteLine(");");
        }

        private void WriteFuncOrActionType(int arity, bool hasReturnType)
        {
            if (hasReturnType)
            {
                Write("System.Func<");
            }
            else
            {
                Write("System.Action");
                if (arity > 0)
                {
                    Write("<");
                }
            }
            for (int j = 0; j < arity; j++)
            {
                if (j > 0)
                {
                    Write(", ");
                }
                Write($"T{j}");
            }

            if (hasReturnType)
            {
                if (arity == 0)
                {
                    Write("R?>");
                }
                else
                {
                    Write(", R?>");
                }
            }
            else
            {
                if (arity > 0)
                {
                    Write(">");
                }
            }
        }

        private void WriteLine()
        {
            WriteLine("");
        }

        private void WriteLineNoIndent(string value)
        {
            _codeBuilder.AppendLine(value);
        }

        private void WriteNoIndent(string value)
        {
            _codeBuilder.Append(value);
        }

        private void Write(string value)
        {
            if (_indent > 0 && _column == 0)
            {
                _codeBuilder.Append(new string(' ', _indent * 4));
            }
            _codeBuilder.Append(value);
            _column += value.Length;
        }

        private void WriteLine(string value)
        {
            if (_indent > 0 && _column == 0)
            {
                _codeBuilder.Append(new string(' ', _indent * 4));
            }
            _codeBuilder.AppendLine(value);
            _column = 0;
        }
    }
}
