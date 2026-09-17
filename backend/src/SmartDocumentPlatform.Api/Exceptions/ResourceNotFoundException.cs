namespace SmartDocumentPlatform.Api.Exceptions;

public sealed class ResourceNotFoundException(string message) : Exception(message);
