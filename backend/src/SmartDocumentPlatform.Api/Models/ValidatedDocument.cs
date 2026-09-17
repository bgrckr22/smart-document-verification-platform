namespace SmartDocumentPlatform.Api.Models;

public sealed record ValidatedDocument(string SafeFileName, string Extension, string ContentType);
