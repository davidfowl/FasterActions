﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public static class Results
{
    public static IResult NotFound() => new StatusCodeResult(404);
    public static IResult Ok() => new StatusCodeResult(200);
    public static IResult Status(int statusCode) => new StatusCodeResult(statusCode);
    public static IResult Ok(object value) => new JsonResult(value);
}