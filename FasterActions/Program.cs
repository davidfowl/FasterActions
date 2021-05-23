using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    public static async ValueTask<Product> Hello(int id, [FromRoute]string p, PageInfo pi) => new() { Message = $"Hello {id}" };
}
class Product
{
    public string Message { get; init; } = default!;
}

record PageInfo(int PageIndex)
{
    public static bool TryParse(string s, out PageInfo? page)
    {
        if (int.TryParse(s, out var value))
        {
            page = new PageInfo(value);
            return true;
        }

        page = default;
        return false;
    }
}