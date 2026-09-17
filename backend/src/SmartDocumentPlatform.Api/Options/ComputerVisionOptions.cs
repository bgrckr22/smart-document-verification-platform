namespace SmartDocumentPlatform.Api.Options;

public sealed class ComputerVisionOptions
{
    public const string SectionName = "ComputerVision";

    public required string BaseUrl { get; init; }
    public int TimeoutSeconds { get; init; } = 60;
}
