using System.ComponentModel.DataAnnotations;

namespace SmartDocumentPlatform.Api.Dtos;

public sealed class UploadDocumentRequest
{
    [Required]
    public required IFormFile File { get; init; }
}
