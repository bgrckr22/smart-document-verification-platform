using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartDocumentPlatform.Api.Data;
using SmartDocumentPlatform.Api.Entities;
using SmartDocumentPlatform.Api.Options;
using SmartDocumentPlatform.Api.Services;
using SmartDocumentPlatform.Api.Tests.Infrastructure;

namespace SmartDocumentPlatform.Api.Tests;

public sealed class DocumentServiceTests
{
    [Fact]
    public async Task ProcessAsync_WhenCompletionSaveFails_RemovesProcessedImageAndRecordsFailure()
    {
        var databaseOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"document-service-{Guid.NewGuid()}")
            .AddInterceptors(new FailCompletedSaveInterceptor())
            .Options;
        await using var dbContext = new AppDbContext(databaseOptions);
        var storage = new InMemoryDocumentStorage();
        var service = new DocumentService(
            dbContext,
            new DocumentFileValidator(Microsoft.Extensions.Options.Options.Create(new UploadOptions())),
            storage,
            new FakeComputerVisionClient(),
            NullLogger<DocumentService>.Instance);
        byte[] imageBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        using var stream = new MemoryStream(imageBytes);
        var file = new FormFile(stream, 0, imageBytes.Length, "file", "document.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(file, CancellationToken.None));

        var document = await dbContext.Documents.SingleAsync();
        Assert.Equal(DocumentProcessingStatus.Failed, document.Status);
        Assert.Null(document.ProcessedImagePath);
        Assert.Null(document.QualityScore);
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await storage.OpenReadAsync($"processed/{document.Id:N}.jpg", CancellationToken.None));
    }

    private sealed class FailCompletedSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<DocumentRecord>()
                .Any(entry => entry.Entity.Status == DocumentProcessingStatus.Completed) == true)
            {
                throw new InvalidOperationException("Simulated completion save failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
