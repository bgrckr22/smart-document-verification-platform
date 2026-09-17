using Microsoft.EntityFrameworkCore;
using SmartDocumentPlatform.Api.Data;
using SmartDocumentPlatform.Api.Dtos;
using SmartDocumentPlatform.Api.Entities;
using SmartDocumentPlatform.Api.Exceptions;
using SmartDocumentPlatform.Api.Models;

namespace SmartDocumentPlatform.Api.Services;

public sealed class DocumentService(
    AppDbContext dbContext,
    DocumentFileValidator fileValidator,
    IDocumentStorage storage,
    IComputerVisionClient computerVisionClient,
    ILogger<DocumentService> logger) : IDocumentService
{
    public async Task<DocumentResponse> ProcessAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var validatedFile = fileValidator.ValidateMetadata(file);
        await using var buffer = new MemoryStream(capacity: checked((int)file.Length));
        await file.CopyToAsync(buffer, cancellationToken);
        var imageBytes = buffer.ToArray();
        fileValidator.ValidateContent(imageBytes, validatedFile.Extension);

        var documentId = Guid.NewGuid();
        var originalImagePath = await storage.SaveAsync(
            "originals",
            documentId,
            validatedFile.Extension,
            imageBytes,
            cancellationToken);

        var document = new DocumentRecord
        {
            Id = documentId,
            OriginalFileName = validatedFile.SafeFileName,
            UploadedAt = DateTimeOffset.UtcNow,
            Status = DocumentProcessingStatus.Processing,
            OriginalImagePath = originalImagePath
        };

        try
        {
            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteFileAsync(originalImagePath, documentId);
            throw;
        }

        try
        {
            var analysis = await computerVisionClient.AnalyzeAsync(
                imageBytes,
                validatedFile.SafeFileName,
                validatedFile.ContentType,
                cancellationToken);

            document.ProcessedImagePath = await storage.SaveAsync(
                "processed",
                document.Id,
                ".jpg",
                analysis.ProcessedImageBytes,
                cancellationToken);
            document.QualityScore = analysis.QualityScore;
            document.BlurDetected = analysis.BlurDetected;
            document.DocumentDetected = analysis.DocumentDetected;
            document.ProcessingTimeMs = analysis.ProcessingTimeMs;
            document.RotationDegrees = analysis.RotationDegrees;
            document.Orientation = analysis.Orientation;
            document.Status = DocumentProcessingStatus.Completed;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Document {DocumentId} processing failed", document.Id);
            var processedImagePath = document.ProcessedImagePath;
            document.ProcessedImagePath = null;
            document.QualityScore = null;
            document.BlurDetected = null;
            document.DocumentDetected = null;
            document.ProcessingTimeMs = null;
            document.RotationDegrees = null;
            document.Orientation = null;
            document.Status = DocumentProcessingStatus.Failed;
            document.ErrorMessage = SafeFailureMessage(exception);
            if (processedImagePath is not null)
            {
                await TryDeleteFileAsync(processedImagePath, document.Id);
            }

            try
            {
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception persistenceException)
            {
                logger.LogError(persistenceException, "Could not record failure for document {DocumentId}", document.Id);
            }
            throw;
        }

        return DocumentResponse.FromEntity(document);
    }

    public async Task<PagedResponse<DocumentResponse>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Documents.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var documents = await query
            .OrderByDescending(item => item.UploadedAt)
            .ThenByDescending(item => item.Id)
            .Skip(skip)
            .Take(take)
            .Select(item => DocumentResponse.FromEntity(item))
            .ToListAsync(cancellationToken);
        return new PagedResponse<DocumentResponse>(documents, total, skip, take);
    }

    public async Task<DocumentResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await FindAsync(id, cancellationToken);
        return DocumentResponse.FromEntity(document);
    }

    public async Task<DocumentFile> OpenImageAsync(
        Guid id,
        bool processed,
        CancellationToken cancellationToken)
    {
        var document = await FindAsync(id, cancellationToken);
        var path = processed ? document.ProcessedImagePath : document.OriginalImagePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ResourceNotFoundException(
                processed ? "The processed image is not available." : "The original image is not available.");
        }

        try
        {
            var stream = await storage.OpenReadAsync(path, cancellationToken);
            return new DocumentFile(stream, ContentTypeFor(path));
        }
        catch (FileNotFoundException exception)
        {
            throw new ResourceNotFoundException(exception.Message);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await FindAsync(id, cancellationToken);
        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var path in new[] { document.OriginalImagePath, document.ProcessedImagePath })
        {
            try
            {
                await storage.DeleteAsync(path, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    exception,
                    "Document {DocumentId} was deleted but stored file {Path} could not be removed",
                    id,
                    path);
            }
        }
    }

    private async Task<DocumentRecord> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Documents.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException($"Document '{id}' was not found.");

    private static string ContentTypeFor(string path) =>
        Path.GetExtension(path).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";

    private async Task TryDeleteFileAsync(string path, Guid documentId)
    {
        try
        {
            await storage.DeleteAsync(path, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Stored file for document {DocumentId} could not be removed during cleanup",
                documentId);
        }
    }

    private static string SafeFailureMessage(Exception exception) => exception switch
    {
        ComputerVisionException or ComputerVisionRejectedException => Truncate(exception.Message, 1000),
        OperationCanceledException => "Document processing was cancelled.",
        IOException or UnauthorizedAccessException => "A document storage error occurred.",
        _ => "Document processing failed unexpectedly."
    };

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
