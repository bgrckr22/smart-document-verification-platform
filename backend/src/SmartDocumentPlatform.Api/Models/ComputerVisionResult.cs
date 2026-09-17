namespace SmartDocumentPlatform.Api.Models;

public sealed record ComputerVisionResult(
    double QualityScore,
    bool BlurDetected,
    bool DocumentDetected,
    long ProcessingTimeMs,
    double? RotationDegrees,
    string Orientation,
    double LaplacianVariance,
    byte[] ProcessedImageBytes,
    string ProcessedContentType);
