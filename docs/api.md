# API Reference

The public API is served by the ASP.NET Core backend. The CV endpoint is an internal service contract used by the backend.

## Backend endpoints

### Upload and analyze a document

`POST /api/documents`

Content type: `multipart/form-data`

| Field | Type | Rules |
|---|---|---|
| `file` | file | Required JPEG or PNG, maximum 10 MB by default |

Successful response: `201 Created`

```json
{
  "id": "16c0a878-d122-4c48-960d-eced5379422f",
  "originalFileName": "receipt.png",
  "uploadedAt": "2026-09-15T09:20:14.512Z",
  "status": "Completed",
  "qualityScore": 86.4,
  "blurDetected": false,
  "documentDetected": true,
  "processingTimeMs": 42,
  "rotationDegrees": -1.5,
  "orientation": "portrait",
  "errorMessage": null,
  "originalImageUrl": "/api/documents/16c0a878-d122-4c48-960d-eced5379422f/original",
  "processedImageUrl": "/api/documents/16c0a878-d122-4c48-960d-eced5379422f/processed"
}
```

Possible errors:

- `400` for a missing, mismatched, empty, or unsupported file.
- `413` when the upload exceeds the configured size limit.
- `422` when the CV service rejects bytes that it cannot process as an image.
- `502` when the CV service times out, cannot be reached, or returns an invalid response. A failed history record is retained for diagnosis.

### List processing history

`GET /api/documents?skip=0&take=20`

- `skip` must be zero or greater.
- `take` must be between 1 and 100.
- Results are sorted by upload date, newest first, with document ID as a stable tie-breaker.

### Get one document

`GET /api/documents/{id}`

Returns `404` when the ID does not exist.

### Read an image

- `GET /api/documents/{id}/original`
- `GET /api/documents/{id}/processed`

The API streams the image with its media type and supports range requests. A processed image is available only after successful analysis.

### Delete a history item

`DELETE /api/documents/{id}`

Returns `204 No Content`. The database record and its local original and processed files are removed.

### Health and API discovery

- `GET /health/live` checks the API process.
- `GET /health/ready` checks the API, its PostgreSQL connection, and the CV service.
- `GET /swagger/index.html` opens the interactive OpenAPI documentation.

## Internal CV endpoint

### Analyze an image

`POST /analyze`

Content type: `multipart/form-data`; field name: `file`.

The FastAPI response uses snake_case because it is a Python service contract. In addition to the public document fields, it returns `laplacian_variance`, image dimensions, output media type, and a base64-encoded corrected image. The backend decodes and stores that image; it does not expose the base64 payload to browsers.

- `415` indicates an unsupported media type.
- `413` indicates an oversized upload.
- `422` indicates bytes that do not match the declared image type or that OpenCV cannot decode.

FastAPI exposes its generated OpenAPI page at the service-local `/docs` route.

## Problem response format

Backend errors use RFC 7807-style problem details:

```json
{
  "type": "about:blank",
  "title": "Invalid document",
  "status": 400,
  "detail": "Only .jpg, .jpeg, and .png files are supported.",
  "instance": "/api/documents",
  "traceId": "0HN..."
}
```
