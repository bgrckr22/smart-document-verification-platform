using SmartDocumentPlatform.Api.Entities;

namespace SmartDocumentPlatform.Api.Dtos;

public sealed record DocumentResponse(
    Guid Id,
    string OriginalFileName,
    DateTimeOffset UploadedAt,
    DocumentProcessingStatus Status,
    double? QualityScore,
    bool? BlurDetected,
    bool? DocumentDetected,
    long? ProcessingTimeMs,
    double? RotationDegrees,
    string? Orientation,
    string? ErrorMessage,
    string OriginalImageUrl,
    string? ProcessedImageUrl)
{
    public static DocumentResponse FromEntity(DocumentRecord document) => new(
        document.Id,
        document.OriginalFileName,
        document.UploadedAt,
        document.Status,
        document.QualityScore,
        document.BlurDetected,
        document.DocumentDetected,
        document.ProcessingTimeMs,
        document.RotationDegrees,
        document.Orientation,
        document.ErrorMessage,
        $"/api/documents/{document.Id}/original",
        document.ProcessedImagePath is null ? null : $"/api/documents/{document.Id}/processed");
}
