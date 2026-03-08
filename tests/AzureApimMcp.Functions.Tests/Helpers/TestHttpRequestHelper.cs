using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AzureApimMcp.Functions.Tests.Helpers;

/// <summary>
/// Helper to create mock HttpRequest objects for Azure Functions testing.
/// </summary>
public static class TestHttpRequestHelper
{
    public static HttpRequest CreateGet(Dictionary<string, string>? queryParams = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";

        if (queryParams != null)
        {
            var queryString = string.Join("&",
                queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            request.QueryString = new QueryString($"?{queryString}");
        }

        return request;
    }

    public static HttpRequest CreatePost<T>(T body)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "POST";
        request.ContentType = "application/json";

        var json = JsonSerializer.Serialize(body);
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        return request;
    }
}
