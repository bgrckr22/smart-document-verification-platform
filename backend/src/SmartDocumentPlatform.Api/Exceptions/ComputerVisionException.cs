namespace SmartDocumentPlatform.Api.Exceptions;

public sealed class ComputerVisionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
