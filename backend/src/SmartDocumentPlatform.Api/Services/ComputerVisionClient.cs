using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SmartDocumentPlatform.Api.Exceptions;
using SmartDocumentPlatform.Api.Models;
using SmartDocumentPlatform.Api.Options;

namespace SmartDocumentPlatform.Api.Services;

public sealed class ComputerVisionClient : IComputerVisionClient
{
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _httpClient;

    public ComputerVisionClient(HttpClient httpClient, IOptions<ComputerVisionOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HealthCheckTimeout);

        try
        {
            using var response = await _httpClient.GetAsync(
                "health",
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<ComputerVisionResult> AnalyzeAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_httpClient.Timeout);
        using var form = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(imageContent, "file", fileName);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "analyze")
            {
                Content = form
            };
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw response.StatusCode switch
                {
                    System.Net.HttpStatusCode.RequestEntityTooLarge => new ComputerVisionRejectedException(
                        "The computer vision service rejected the image because it was too large."),
                    System.Net.HttpStatusCode.UnsupportedMediaType => new ComputerVisionRejectedException(
                        "The computer vision service rejected the image media type."),
                    System.Net.HttpStatusCode.UnprocessableEntity => new ComputerVisionRejectedException(
                        "OpenCV could not decode or process the uploaded image."),
                    _ => new ComputerVisionException(
                        $"The computer vision service returned status {(int)response.StatusCode}.")
                };
            }

            var payload = await response.Content.ReadFromJsonAsync<ComputerVisionResponse>(
                cancellationToken: timeout.Token)
                ?? throw new ComputerVisionException("The computer vision service returned an empty response.");

            byte[] processedBytes;
            try
            {
                processedBytes = Convert.FromBase64String(payload.ProcessedImageBase64);
            }
            catch (FormatException exception)
            {
                throw new ComputerVisionException(
                    "The computer vision service returned an invalid processed image.", exception);
            }

            if (payload.QualityScore is < 0 or > 100
                || !double.IsFinite(payload.QualityScore)
                || payload.ProcessingTimeMs < 0
                || payload.ProcessedWidth <= 0
                || payload.ProcessedHeight <= 0
                || payload.LaplacianVariance < 0
                || !double.IsFinite(payload.LaplacianVariance)
                || payload.RotationDegrees is double rotation && !double.IsFinite(rotation)
                || payload.Orientation is not ("portrait" or "landscape" or "square")
                || !string.Equals(payload.ProcessedContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                || processedBytes.Length < 3
                || processedBytes[0] != 0xFF
                || processedBytes[1] != 0xD8
                || processedBytes[2] != 0xFF)
            {
                throw new ComputerVisionException(
                    "The computer vision service returned an invalid analysis response.");
            }

            return new ComputerVisionResult(
                payload.QualityScore,
                payload.BlurDetected,
                payload.DocumentDetected,
                payload.ProcessingTimeMs,
                payload.RotationDegrees,
                payload.Orientation,
                payload.LaplacianVariance,
                processedBytes,
                payload.ProcessedContentType);
        }
        catch (HttpRequestException exception)
        {
            throw new ComputerVisionException("The computer vision service is unavailable.", exception);
        }
        catch (JsonException exception)
        {
            throw new ComputerVisionException("The computer vision service returned an invalid analysis response.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ComputerVisionException("The computer vision service timed out.", exception);
        }
    }

    private sealed record ComputerVisionResponse(
        [property: JsonRequired, JsonPropertyName("quality_score")] double QualityScore,
        [property: JsonRequired, JsonPropertyName("blur_detected")] bool BlurDetected,
        [property: JsonRequired, JsonPropertyName("document_detected")] bool DocumentDetected,
        [property: JsonRequired, JsonPropertyName("processing_time_ms")] long ProcessingTimeMs,
        [property: JsonRequired, JsonPropertyName("rotation_degrees")] double? RotationDegrees,
        [property: JsonRequired, JsonPropertyName("orientation")] string Orientation,
        [property: JsonRequired, JsonPropertyName("laplacian_variance")] double LaplacianVariance,
        [property: JsonRequired, JsonPropertyName("processed_image_base64")] string ProcessedImageBase64,
        [property: JsonRequired, JsonPropertyName("processed_content_type")] string ProcessedContentType,
        [property: JsonRequired, JsonPropertyName("processed_width")] int ProcessedWidth,
        [property: JsonRequired, JsonPropertyName("processed_height")] int ProcessedHeight);
}
