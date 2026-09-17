namespace SmartDocumentPlatform.Api.Options;

public sealed class UploadOptions
{
    public const string SectionName = "Uploads";

    public long MaxBytes { get; init; } = 10 * 1024 * 1024;
}
