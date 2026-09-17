namespace SmartDocumentPlatform.Api.Services;

public interface IDocumentStorage
{
    Task<string> SaveAsync(
        string category,
        Guid documentId,
        string extension,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
    Task DeleteAsync(string? relativePath, CancellationToken cancellationToken);
}
