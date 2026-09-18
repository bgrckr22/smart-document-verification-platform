using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SmartDocumentPlatform.Api.Exceptions;
using SmartDocumentPlatform.Api.Options;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Tests;

public sealed class DocumentFileValidatorTests
{
    private readonly DocumentFileValidator _validator = new(
        Microsoft.Extensions.Options.Options.Create(new UploadOptions { MaxBytes = 1024 }));

    [Fact]
    public void Validate_AcceptsPngWithMatchingSignature()
    {
        byte[] content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        var file = FormFile("scan.png", "image/png", content);

        var result = _validator.Validate(file, content);

        Assert.Equal("scan.png", result.SafeFileName);
        Assert.Equal(".png", result.Extension);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public void Validate_RejectsExtensionAndContentTypeMismatch()
    {
        byte[] content = [0xFF, 0xD8, 0xFF, 0x00];
        var file = FormFile("scan.jpg", "image/png", content);

        var exception = Assert.Throws<AppValidationException>(() => _validator.Validate(file, content));

        Assert.Contains("do not match", exception.Message);
    }

    [Fact]
    public void Validate_RejectsFileAboveConfiguredLimit()
    {
        var content = new byte[1025];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;
        var file = FormFile("scan.jpg", "image/jpeg", content);

        var exception = Assert.Throws<PayloadTooLargeException>(() => _validator.Validate(file, content));

        Assert.Contains("smaller", exception.Message);
    }

    [Fact]
    public void Validate_RemovesClientPathAndControlCharactersFromFileName()
    {
        byte[] content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var file = FormFile("C:\\fakepath\\rece\r\nipt.png", "image/png", content);

        var result = _validator.Validate(file, content);

        Assert.Equal("receipt.png", result.SafeFileName);
    }

    [Fact]
    public void Validate_TruncatesFileNameToDatabaseLimit()
    {
        byte[] content = [0xFF, 0xD8, 0xFF];
        var file = FormFile($"{new string('a', 300)}.jpg", "image/jpeg", content);

        var result = _validator.Validate(file, content);

        Assert.Equal(255, result.SafeFileName.Length);
        Assert.EndsWith(".jpg", result.SafeFileName);
    }

    private static FormFile FormFile(string name, string contentType, byte[] content)
    {
        var file = new FormFile(new MemoryStream(content), 0, content.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
        return file;
    }
}
