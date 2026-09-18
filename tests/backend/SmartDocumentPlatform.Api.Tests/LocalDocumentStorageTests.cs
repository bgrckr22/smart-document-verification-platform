using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SmartDocumentPlatform.Api.Options;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Tests;

public sealed class LocalDocumentStorageTests
{
    [Fact]
    public async Task DeleteAsync_RejectsPathOutsideStorageRoot()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"smart-document-storage-{Guid.NewGuid():N}");
        try
        {
            var storage = new LocalDocumentStorage(
                Microsoft.Extensions.Options.Options.Create(new FileStorageOptions { RootPath = rootPath }),
                new TestWebHostEnvironment());

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await storage.DeleteAsync("../outside.jpg", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath);
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SmartDocumentPlatform.Api.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
