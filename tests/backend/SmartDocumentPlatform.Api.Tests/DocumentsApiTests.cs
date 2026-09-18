using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartDocumentPlatform.Api.Tests.Infrastructure;

namespace SmartDocumentPlatform.Api.Tests;

public sealed class DocumentsApiTests
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task UploadListReadAndDelete_CompletesDocumentLifecycle()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new("image/png");
        form.Add(fileContent, "file", "receipt.png");

        var uploadResponse = await client.PostAsync("/api/documents", form);
        var uploadedJson = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        Assert.Equal("Completed", uploadedJson.GetProperty("status").GetString());
        Assert.Equal(86.4, uploadedJson.GetProperty("qualityScore").GetDouble());
        var documentId = uploadedJson.GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/documents");
        Assert.Equal(1, list.GetProperty("total").GetInt32());

        var originalResponse = await client.GetAsync($"/api/documents/{documentId}/original");
        Assert.Equal(HttpStatusCode.OK, originalResponse.StatusCode);
        Assert.Equal(TinyPng, await originalResponse.Content.ReadAsByteArrayAsync());

        var deleteResponse = await client.DeleteAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missingResponse = await client.GetAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_WithUnsupportedExtension_ReturnsProblemDetails()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new("text/plain");
        form.Add(fileContent, "file", "notes.txt");

        var response = await client.PostAsync("/api/documents", form);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Invalid document", problem.GetProperty("title").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Upload_WhenFileExceedsConfiguredLimit_ReturnsPayloadTooLarge()
    {
        await using var factory = new ApiFactory(maximumUploadBytes: 1024);
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        var content = new byte[1025];
        Array.Copy(TinyPng, content, TinyPng.Length);
        using var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new("image/png");
        form.Add(fileContent, "file", "large.png");

        var response = await client.PostAsync("/api/documents", form);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("Document too large", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Upload_WhenCvServiceFails_RetainsFailedHistoryRecord()
    {
        await using var factory = new ApiFactory(simulateCvFailure: true);
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new("image/png");
        form.Add(fileContent, "file", "receipt.png");

        var uploadResponse = await client.PostAsync("/api/documents", form);
        var history = await client.GetFromJsonAsync<JsonElement>("/api/documents");
        var failedDocument = history.GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.BadGateway, uploadResponse.StatusCode);
        Assert.Equal("Failed", failedDocument.GetProperty("status").GetString());
        Assert.Equal("Simulated CV service failure.", failedDocument.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task Readiness_WhenDependenciesAreHealthy_ReturnsDependencyStatus()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("connected", body.GetProperty("database").GetString());
        Assert.Equal("connected", body.GetProperty("computerVision").GetString());
    }
}
