using SmartDocumentPlatform.Api.Dtos;
using SmartDocumentPlatform.Api.Models;

namespace SmartDocumentPlatform.Api.Services;

public interface IDocumentService
{
    Task<DocumentResponse> ProcessAsync(IFormFile file, CancellationToken cancellationToken);
    Task<PagedResponse<DocumentResponse>> ListAsync(int skip, int take, CancellationToken cancellationToken);
    Task<DocumentResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<DocumentFile> OpenImageAsync(Guid id, bool processed, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
