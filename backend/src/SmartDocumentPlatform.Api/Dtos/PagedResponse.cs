namespace SmartDocumentPlatform.Api.Dtos;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);
