using Microsoft.Extensions.Options;
using SmartDocumentPlatform.Api.Options;

namespace SmartDocumentPlatform.Api.Services;

public sealed class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _rootPath;

    public LocalDocumentStorage(IOptions<FileStorageOptions> options, IWebHostEnvironment environment)
    {
        var configuredPath = options.Value.RootPath;
        _rootPath = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        string category,
        Guid documentId,
        string extension,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var safeCategory = category switch
        {
            "originals" => category,
            "processed" => category,
            _ => throw new ArgumentOutOfRangeException(nameof(category), "Unknown storage category.")
        };

        var directory = Path.Combine(_rootPath, safeCategory);
        Directory.CreateDirectory(directory);
        var relativePath = Path.Combine(safeCategory, $"{documentId:N}{extension}");
        var absolutePath = ResolveSafePath(relativePath);
        var temporaryPath = absolutePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, absolutePath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return relativePath.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = ResolveSafePath(relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The stored document image could not be found.");
        }

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string? relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            var absolutePath = ResolveSafePath(relativePath);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }

        return Task.CompletedTask;
    }

    private string ResolveSafePath(string relativePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!absolutePath.StartsWith(rootWithSeparator, pathComparison))
        {
            throw new InvalidOperationException("A storage path resolved outside the configured root.");
        }

        return absolutePath;
    }
}
