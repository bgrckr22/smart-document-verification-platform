using SmartDocumentPlatform.Api.Exceptions;
using SmartDocumentPlatform.Api.Models;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Tests.Infrastructure;

public sealed class FailingComputerVisionClient : IComputerVisionClient
{
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<ComputerVisionResult> AnalyzeAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        CancellationToken cancellationToken) =>
        Task.FromException<ComputerVisionResult>(
            new ComputerVisionException("Simulated CV service failure."));
}
