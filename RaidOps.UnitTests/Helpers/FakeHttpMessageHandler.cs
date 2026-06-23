using System.Net;
using System.Text;

namespace RaidOps.UnitTests.Helpers;

/// <summary>
/// Fake HttpMessageHandler that returns a preset response for all requests.
/// Captures the last sent request for assertion purposes.
/// The request body is read eagerly to avoid ObjectDisposedException
/// when the caller inspects it after HttpClient disposes the content.
/// </summary>
internal class FakeHttpMessageHandler(
    HttpStatusCode statusCode,
    string? content = null,
    Exception? exceptionToThrow = null) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>Body of the last sent request, pre-read before HttpClient disposes the content.</summary>
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        if (exceptionToThrow is not null)
            throw exceptionToThrow;

        var response = new HttpResponseMessage(statusCode);
        if (content is not null)
            response.Content = new StringContent(content, Encoding.UTF8, "application/json");
        return response;
    }
}
