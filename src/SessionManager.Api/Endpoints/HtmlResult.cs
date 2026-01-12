using Microsoft.AspNetCore.Http;

namespace SessionManager.Api.Endpoints;

public class HtmlResult : IResult
{
    private readonly string _html;
    private readonly int _statusCode;

    public HtmlResult(string html, int statusCode = 200)
    {
        _html = html;
        _statusCode = statusCode;
    }

    public async Task ExecuteAsync(HttpContext HttpContext)
    {
        HttpContext.Response.ContentType = "text/html; charset=utf-8";
        HttpContext.Response.StatusCode = _statusCode;
        await HttpContext.Response.WriteAsync(_html);
    }
}
