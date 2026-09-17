using System.Globalization;
using Microsoft.Extensions.Options;
using SmartDocumentPlatform.Api.Exceptions;
using SmartDocumentPlatform.Api.Models;
using SmartDocumentPlatform.Api.Options;

namespace SmartDocumentPlatform.Api.Services;

public sealed class DocumentFileValidator(IOptions<UploadOptions> options)
{
    private const int MaximumFileNameLength = 255;
    private static readonly IReadOnlyDictionary<string, string> AllowedFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png"
        };

    public ValidatedDocument Validate(IFormFile file, ReadOnlySpan<byte> bytes)
    {
        var validatedDocument = ValidateMetadata(file);
        ValidateContent(bytes, validatedDocument.Extension);
        return validatedDocument;
    }

    public ValidatedDocument ValidateMetadata(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new AppValidationException("Choose a non-empty JPEG or PNG image.");
        }

        if (file.Length > options.Value.MaxBytes)
        {
            throw FileTooLarge();
        }

        var safeFileName = SanitizeFileName(file.FileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!AllowedFiles.TryGetValue(extension, out var expectedContentType))
        {
            throw new AppValidationException("Only .jpg, .jpeg, and .png files are supported.");
        }

        if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppValidationException("The file extension and content type do not match.");
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(safeFileName)))
        {
            safeFileName = $"document{extension}";
        }

        return new ValidatedDocument(safeFileName, extension, expectedContentType);
    }

    public void ValidateContent(ReadOnlySpan<byte> bytes, string extension)
    {
        if (bytes.IsEmpty)
        {
            throw new AppValidationException("Choose a non-empty JPEG or PNG image.");
        }

        if (bytes.Length > options.Value.MaxBytes)
        {
            throw FileTooLarge();
        }

        if (!HasExpectedSignature(bytes, extension))
        {
            throw new AppValidationException("The file content is not a valid JPEG or PNG image.");
        }
    }

    private static string SanitizeFileName(string clientFileName)
    {
        var normalized = (clientFileName ?? string.Empty).Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        fileName = string.Concat(fileName.Where(character => !char.IsControl(character))).Trim();

        if (fileName.Length <= MaximumFileNameLength)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var maximumStemLength = MaximumFileNameLength - extension.Length;
        var textElementOffsets = StringInfo.ParseCombiningCharacters(stem);
        var endOffset = textElementOffsets.LastOrDefault(offset => offset <= maximumStemLength);
        return stem[..endOffset] + extension;
    }

    private PayloadTooLargeException FileTooLarge()
    {
        var limitInMegabytes = options.Value.MaxBytes / (1024d * 1024d);
        return new PayloadTooLargeException($"The image must be {limitInMegabytes:0.##} MB or smaller.");
    }

    private static bool HasExpectedSignature(ReadOnlySpan<byte> bytes, string extension)
    {
        ReadOnlySpan<byte> jpegSignature = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return extension is ".jpg" or ".jpeg"
            ? bytes.StartsWith(jpegSignature)
            : bytes.StartsWith(pngSignature);
    }
}
