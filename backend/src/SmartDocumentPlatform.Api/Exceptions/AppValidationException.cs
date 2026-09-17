namespace SmartDocumentPlatform.Api.Exceptions;

public sealed class AppValidationException(string message) : Exception(message);
