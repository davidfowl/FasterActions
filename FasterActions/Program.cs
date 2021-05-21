using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// Boot the web application
var app = WebApplication.Create(args);


// Create a RequestDelegate, the ASP.NET primitive for handling requests from the
// specified delegate
RequestDelegate rd = RequestDelegateFactory2.CreateRequestDelegate(typeof(Foo).GetMethod(nameof(Foo.Hello))!);

// Map this delegate to the path /
app.MapGet("/", rd);

// Run the application
app.Run();

class Foo
{
    public static async ValueTask<Product> Hello(int id) => new() { Message = $"Hello {id}" };
}
class Product
{
    public string Message { get; init; } = default!;
}