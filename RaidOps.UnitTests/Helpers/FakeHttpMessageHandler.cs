using System.Net;
using System.Text;

namespace RaidOps.UnitTests.Helpers;

/// <summary>
/// Fake HttpMessageHandler that returns a preset response for all requests.
/// Captures the last sent request for assertion purposes.
/// </summary>
internal class FakeHttpMessageHandler(HttpStatusCode statusCode, string? content = null) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(statusCode);
        if (content is not null)
            response.Content = new StringContent(content, Encoding.UTF8, "application/json");
        return Task.FromResult(response);
    }
}
