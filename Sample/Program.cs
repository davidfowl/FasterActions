using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// Boot the web application
var app = WebApplication.Create(args);

var options = new DbContextOptionsBuilder().UseSqlite("Data Source=Todos.db").Options;

// This makes sure the database and tables are created
using (var db = new TodoDbContext(options))
{
    db.Database.EnsureCreated();
}

// Register the routes
TodoApi.MapRoutes(app, options);

// Create a RequestDelegate, the ASP.NET primitive for handling requests from the
// specified delegate
RequestDelegate rd = RequestDelegateFactory2.CreateRequestDelegate(typeof(Foo).GetMethod(nameof(Foo.Hello))!);

// Map this delegate to the path /
app.MapGet("/", rd);

// Run the application
app.Run();


class Foo
{
    public static async ValueTask<Data> Hello(string name, Options options, PageInfo pi) => new() { Message = $"Hello {name}" };
}

class Data
{
    public string Message { get; init; } = default!;
}

enum Options
{
    One,
    Two
}

record PageInfo(int PageIndex)
{
    public static bool TryParse(string s, out PageInfo page)
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