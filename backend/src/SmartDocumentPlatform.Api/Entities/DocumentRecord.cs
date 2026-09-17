namespace SmartDocumentPlatform.Api.Entities;

public sealed class DocumentRecord
{
    public Guid Id { get; set; }
    public required string OriginalFileName { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DocumentProcessingStatus Status { get; set; }
    public double? QualityScore { get; set; }
    public bool? BlurDetected { get; set; }
    public bool? DocumentDetected { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public required string OriginalImagePath { get; set; }
    public string? ProcessedImagePath { get; set; }
    public double? RotationDegrees { get; set; }
    public string? Orientation { get; set; }
    public string? ErrorMessage { get; set; }
}
