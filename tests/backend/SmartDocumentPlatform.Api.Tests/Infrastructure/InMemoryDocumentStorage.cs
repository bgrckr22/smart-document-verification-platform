using System.Collections.Concurrent;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Tests.Infrastructure;

public sealed class InMemoryDocumentStorage : IDocumentStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public Task<string> SaveAsync(
        string category,
        Guid documentId,
        string extension,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var path = $"{category}/{documentId:N}{extension}";
        _files[path] = content.ToArray();
        return Task.FromResult(path);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(relativePath, out var content))
        {
            throw new FileNotFoundException();
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    public Task DeleteAsync(string? relativePath, CancellationToken cancellationToken)
    {
        if (relativePath is not null)
        {
            _files.TryRemove(relativePath, out _);
        }

        return Task.CompletedTask;
    }
}
