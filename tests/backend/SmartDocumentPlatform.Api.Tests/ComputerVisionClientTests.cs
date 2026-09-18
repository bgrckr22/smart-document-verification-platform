using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SmartDocumentPlatform.Api.Exceptions;
using SmartDocumentPlatform.Api.Options;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Tests;

public sealed class ComputerVisionClientTests
{
    [Fact]
    public async Task AnalyzeAsync_MapsValidFastApiResponse()
    {
        var handler = new StubHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://cv.test/analyze", request.RequestUri?.ToString());
            Assert.StartsWith("multipart/form-data", request.Content?.Headers.ContentType?.ToString());
            var requestBody = await request.Content!.ReadAsStringAsync();
            Assert.Contains("filename=scan.png", requestBody);

            const string json = """
                {
                  "quality_score": 82.5,
                  "blur_detected": false,
                  "document_detected": true,
                  "processing_time_ms": 24,
                  "rotation_degrees": -1.2,
                  "orientation": "portrait",
                  "laplacian_variance": 280.4,
                  "processed_image_base64": "/9j/AA==",
                  "processed_content_type": "image/jpeg",
                  "processed_width": 1,
                  "processed_height": 1
                }
                """;
            return JsonResponse(HttpStatusCode.OK, json);
        });
        var client = CreateClient(handler);

        var result = await client.AnalyzeAsync(
            [0x89, 0x50, 0x4E, 0x47],
            "scan.png",
            "image/png",
            CancellationToken.None);

        Assert.Equal(82.5, result.QualityScore);
        Assert.Equal("portrait", result.Orientation);
        Assert.Equal([0xFF, 0xD8, 0xFF, 0x00], result.ProcessedImageBytes);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotExposeRawErrorResponse()
    {
        var handler = new StubHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.InternalServerError,
            "{\"detail\":\"database password was secret\"}")));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ComputerVisionException>(() => client.AnalyzeAsync(
            [0xFF, 0xD8, 0xFF],
            "scan.jpg",
            "image/jpeg",
            CancellationToken.None));

        Assert.Equal("The computer vision service returned status 500.", exception.Message);
        Assert.DoesNotContain("password", exception.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_MapsUnprocessableImageToSafeClientError()
    {
        var handler = new StubHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.UnprocessableEntity,
            "{\"detail\":\"internal path /service/private\"}")));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ComputerVisionRejectedException>(() => client.AnalyzeAsync(
            [0xFF, 0xD8, 0xFF],
            "scan.jpg",
            "image/jpeg",
            CancellationToken.None));

        Assert.Equal("OpenCV could not decode or process the uploaded image.", exception.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsIncompleteFastApiResponse()
    {
        var handler = new StubHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            "{\"quality_score\":82,\"processed_image_base64\":\"/9j/AA==\"}")));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ComputerVisionException>(() => client.AnalyzeAsync(
            [0xFF, 0xD8, 0xFF],
            "scan.jpg",
            "image/jpeg",
            CancellationToken.None));

        Assert.Equal("The computer vision service returned an invalid analysis response.", exception.Message);
    }

    private static ComputerVisionClient CreateClient(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Microsoft.Extensions.Options.Options.Create(new ComputerVisionOptions
        {
            BaseUrl = "http://cv.test",
            TimeoutSeconds = 5
        }));

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }
}
