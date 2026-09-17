namespace SmartDocumentPlatform.Api.Exceptions;

public sealed class ComputerVisionRejectedException(string message)
    : Exception(message);
