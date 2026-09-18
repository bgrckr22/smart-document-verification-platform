using SmartDocumentPlatform.Api.Models;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Tests.Infrastructure;

public sealed class FakeComputerVisionClient : IComputerVisionClient
{
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<ComputerVisionResult> AnalyzeAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ComputerVisionResult(
            QualityScore: 86.4,
            BlurDetected: false,
            DocumentDetected: true,
            ProcessingTimeMs: 42,
            RotationDegrees: -1.5,
            Orientation: "portrait",
            LaplacianVariance: 420.0,
            ProcessedImageBytes: imageBytes,
            ProcessedContentType: "image/jpeg"));
}
