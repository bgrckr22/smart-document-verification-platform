namespace SmartDocumentPlatform.Api.Exceptions;

public sealed class PayloadTooLargeException(string message) : Exception(message);
