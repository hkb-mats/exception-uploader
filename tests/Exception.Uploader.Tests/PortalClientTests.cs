using System.Net;
using System.Text;
using Exception.Uploader.Core;
using FluentAssertions;

namespace Exception.Uploader.Tests;

public class PortalClientTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        public Func<HttpResponseMessage> Respond { get; init; } = () => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Respond();
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static async Task<(UploadResult Result, Handler Handler)> UploadAsync(Func<HttpResponseMessage> respond)
    {
        var handler = new Handler { Respond = respond };
        var client = new PortalClient(new HttpClient(handler) { BaseAddress = new Uri("https://portal.test/") });
        return (await client.UploadAsync("exu_token", "EXJ1:abc", CancellationToken.None), handler);
    }

    [Fact]
    public async Task SendsOnlyTheStringWithTheTokenInItsHeader()
    {
        var (result, handler) = await UploadAsync(() => Json(HttpStatusCode.OK, """{"level":12,"entriesAdded":3}"""));

        result.Status.Should().Be(UploadStatus.Uploaded);
        result.Level.Should().Be(12);
        handler.Request!.RequestUri.Should().Be("https://portal.test/api/addon-uploads/journal");
        handler.Request.Method.Should().Be(HttpMethod.Post);
        handler.Request.Headers.GetValues("X-Upload-Token").Should().Equal("exu_token");
        handler.Body.Should().Be("""{"data":"EXJ1:abc"}""");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, UploadStatus.TokenRefused)]
    [InlineData(HttpStatusCode.TooManyRequests, UploadStatus.Retry)]
    [InlineData(HttpStatusCode.InternalServerError, UploadStatus.Retry)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UploadStatus.Retry)]
    [InlineData(HttpStatusCode.BadRequest, UploadStatus.Rejected)]
    [InlineData(HttpStatusCode.NotFound, UploadStatus.Rejected)]
    [InlineData(HttpStatusCode.Conflict, UploadStatus.Rejected)]
    public async Task SortsResponses(HttpStatusCode status, UploadStatus expected)
    {
        var (result, _) = await UploadAsync(() => new HttpResponseMessage(status));

        result.Status.Should().Be(expected);
    }

    [Fact]
    public async Task ShowsThePortalsReason()
    {
        var (result, _) = await UploadAsync(() => Json(HttpStatusCode.NotFound, """{"detail":"Krise isn't on your portal account."}"""));

        result.Message.Should().Be("Krise isn't on your portal account.");
    }

    [Fact]
    public async Task ANetworkFailureRetries()
    {
        var (result, _) = await UploadAsync(() => throw new HttpRequestException("down"));

        result.Status.Should().Be(UploadStatus.Retry);
    }
}
