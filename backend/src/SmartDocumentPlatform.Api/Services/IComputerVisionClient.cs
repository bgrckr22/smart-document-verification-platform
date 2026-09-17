using SmartDocumentPlatform.Api.Models;

namespace SmartDocumentPlatform.Api.Services;

public interface IComputerVisionClient
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);

    Task<ComputerVisionResult> AnalyzeAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);
}
