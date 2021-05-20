using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// Boot the web application
var app = WebApplication.Create(args);


// Create a RequestDelegate, the ASP.NET primitive for handling requests from the
// specified delegate
RequestDelegate rd = ReflectionbasedRequestDelegateFactory.CreateRequestDelegate(typeof(Foo).GetMethod(nameof(Foo.Hello))!);

// Map this delegate to the path /
app.MapGet("/", rd);

// Run the application
app.Run();

class Foo
{
    public static object Hello(string name) => new { Message = $"Hello {name}" };
}