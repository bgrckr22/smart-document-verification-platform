# Smart Document Verification Platform - Implementation Plan

## Product scope

Build a portfolio-ready document processing application that accepts JPEG and PNG images, sends them through a real OpenCV pipeline, stores the result and processing metadata in PostgreSQL, and presents uploads and history in a responsive React interface.

The code should stay approachable for a junior engineer: small classes, explicit names, limited abstraction, and comments only where the reason for a decision is not obvious from the code.

## Architecture decisions

- Use one ASP.NET Core Web API project. Entity Framework Core already provides repository and unit-of-work behavior, so a second generic repository layer would add ceremony without useful isolation.
- Keep orchestration in `DocumentService`, file rules in `DocumentFileValidator`, external HTTP details in `ComputerVisionClient`, and storage behind `IDocumentStorage` so Azure Blob Storage can replace local storage later.
- Run image analysis in an independent FastAPI service. It returns analysis metadata plus the perspective-corrected image to the backend; the browser never calls this internal service directly.
- Use React with TypeScript and no large UI framework. The frontend calls the local backend on port 5000; Vite also provides a same-origin fallback proxy. Nginx remains part of the optional Docker image.
- Use database migrations at application startup. Native scripts start project-local PostgreSQL before the API; optional Docker Compose uses health-based ordering.
- Store uploaded files on local disk during native development and in a named volume under Docker. In Azure, replace that implementation with Blob Storage while keeping the service contract unchanged.

## Delivery stages

### 1. Repository foundation

- Create the requested folder layout and solution metadata.
- Add `.gitignore`, `.env.example`, MIT license, and contributor guidance.
- Define native Windows start scripts, safe example configuration, and optional Docker Compose services.

Acceptance checks:

- Every path referenced by Compose exists.
- Example environment values document every local setting without containing a usable secret.

### 2. Computer vision service

- Add the FastAPI application and `POST /analyze` multipart endpoint.
- Implement decoding, grayscale conversion, Gaussian blur, Canny edges, contour selection, four-point perspective correction, Laplacian blur measurement, a documented heuristic quality score, and orientation details.
- Add focused pytest coverage using generated images so tests have no binary fixture dependency.
- Add a production container image and service health endpoint.

Acceptance checks:

- Sharp synthetic documents are detected and corrected.
- Blurred images are classified as blurry.
- Invalid uploads return structured HTTP errors.
- Python tests pass in the service container or a local Python environment.

### 3. ASP.NET Core API

- Add the .NET 8 API, EF Core PostgreSQL context, initial migration, DTOs, application services, storage abstraction, typed CV client, validation, and centralized problem responses.
- Implement create, list, get, original-image, processed-image, and delete endpoints.
- Add OpenAPI/Swagger and liveness/readiness endpoints.
- Add unit and API integration tests with test-only in-memory dependencies.

Acceptance checks:

- Solution builds without warnings that indicate broken code.
- Unit and integration tests pass.
- API persists completed and failed processing states correctly.
- Image endpoints return the stored bytes and delete removes the history record.

### 4. React frontend

- Add a typed API client and a responsive dashboard.
- Implement drag-and-drop/file-picker upload, client-side file validation, original/processed comparison, analysis metrics, history refresh, and delete.
- Add accessible loading, empty, success, and error states.
- Add a multi-stage production image with Nginx proxying API traffic.

Acceptance checks:

- TypeScript type checking and the production build pass.
- The UI remains usable on desktop and mobile widths.
- Upload, selection, refresh, and deletion state transitions are consistent.

### 5. Documentation and end-to-end verification

- Write the README, Mermaid architecture diagram, API reference, local setup, troubleshooting, Azure mapping, screenshots checklist, and roadmap.
- Restore and build every service, then start the native Windows stack.
- Verify PostgreSQL readiness, CV health, API health, an actual upload, history retrieval, image retrieval, and deletion.
- Re-run backend and Python tests after integration fixes.

Acceptance checks:

- The complete system starts without Docker on ports 5173, 5000, 8000, and 5432.
- An uploaded generated sample flows through frontend/API/CV/PostgreSQL and produces a corrected image.
- Optional Docker Compose files remain valid for CI and future deployment work.
- Final commands and recommended GitHub screenshots are documented and reported.

## Intended repository layout

```text
smart-document-platform/
|-- backend/
|   |-- src/SmartDocumentPlatform.Api/
|   `-- SmartDocumentPlatform.sln
|-- cv-service/
|   |-- app/
|   `-- Dockerfile
|-- frontend/
|   |-- src/
|   `-- Dockerfile
|-- tests/
|   |-- backend/SmartDocumentPlatform.Api.Tests/
|   `-- cv-service/
|-- docs/
|-- docker-compose.yml
`-- README.md
```

## Deferred scope

Authentication, OCR, classification, tamper detection, antivirus scanning, distributed queues, and Azure deployment are future roadmap work. The project will not label heuristic computer-vision output as machine learning.
