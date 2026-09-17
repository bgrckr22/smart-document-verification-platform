namespace SmartDocumentPlatform.Api.Options;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public required string RootPath { get; init; }
}
