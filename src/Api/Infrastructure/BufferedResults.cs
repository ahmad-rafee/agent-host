using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AgentHost.Api.Infrastructure;

internal static class BufferedResults
{
    public static IResult Ok<T>(T value) => new BufferedJsonResult<T>(value, StatusCodes.Status200OK);
    public static IResult Created<T>(string location, T value) => new BufferedJsonResult<T>(value, StatusCodes.Status201Created, location);
    public static IResult NotFound<T>(T value) => new BufferedJsonResult<T>(value, StatusCodes.Status404NotFound);
    public static IResult BadRequest<T>(T value) => new BufferedJsonResult<T>(value, StatusCodes.Status400BadRequest);
    public static IResult Problem(string detail) => new BufferedJsonResult<object>(new { error = detail }, StatusCodes.Status500InternalServerError);

    private sealed class BufferedJsonResult<T> : IResult
    {
        private readonly T _value;
        private readonly int _statusCode;
        private readonly string? _location;

        public BufferedJsonResult(T value, int statusCode, string? location = null)
        {
            _value = value;
            _statusCode = statusCode;
            _location = location;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = _statusCode;
            if (_location != null)
            {
                httpContext.Response.Headers.Location = _location;
            }
            httpContext.Response.ContentType = "application/json";

            var jsonOptions = httpContext.RequestServices.GetService(typeof(IOptions<JsonOptions>)) as IOptions<JsonOptions>;
            var serializerOptions = jsonOptions?.Value?.SerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

            var json = JsonSerializer.Serialize(_value, serializerOptions);
            await httpContext.Response.WriteAsync(json);
        }
    }
}
