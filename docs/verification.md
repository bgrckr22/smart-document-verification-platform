# Verification Record

Verification performed on 16 September 2026.

| Check | Result |
|---|---|
| ASP.NET Core Release build and xUnit/API suite | Passed: 16 tests, 0 build warnings |
| EF Core formatting and analyzer pass | Passed with no changes required |
| EF Core migration generation | Passed with `dotnet-ef` 8.0.11 |
| EF Core migrations against local PostgreSQL | Passed; `InitialCreate` and `StableDocumentHistory` applied, with no pending model changes |
| PostgreSQL index | Passed; `(uploaded_at DESC, id DESC)` matches stable history ordering |
| Local PostgreSQL connectivity | Passed with project-local PostgreSQL 16.15 on `localhost:5432`; development database `smart_document_db` |
| FastAPI/OpenCV pytest suite | Passed: 17 tests |
| Python dependency consistency | Passed: no broken requirements in an isolated Python 3.12 environment |
| React TypeScript production build | Passed with Vite 8.3.0 |
| npm dependency audit | Passed: 0 vulnerabilities |
| NuGet direct and transitive audit | Passed: 0 vulnerable packages |
| Native end-to-end smoke test | Passed: upload, OpenCV analysis, image retrieval, database persistence, history, and deletion |
| Browser dashboard review | Passed: current result, original and processed images, loading state, history, and backend Swagger link rendered |
| Local HTTP checks | Passed: frontend, Swagger, FastAPI docs, backend readiness, and CORS |
| Upload and storage security | Passed: type, signature, and size checks; bounded CV multipart body; path traversal checks use the host file system's case rules |
| Optional Docker Compose and CI YAML parsing | Passed |
| Browser visual check | Passed at desktop and mobile breakpoints |

The backend HTTP tests exercise upload, validation, persistence, list, image retrieval, deletion, problem details, failed processing records, processed-file cleanup, and path traversal protection. The CV tests use generated image arrays and exercise the real OpenCV implementation, including multipart size limits and temporary-file closure.

The `smart_document_db` database was created with the existing local account, both EF Core migrations were applied, and the `documents` and `__EFMigrationsHistory` tables were verified in PostgreSQL. A synthetic upload was created and read through the backend API, then confirmed directly in PostgreSQL as `Completed` with quality metrics and a processed image path. The database password remains only in the Git-ignored `.env` file.

The current application has no authentication or per-user access control. Keep this development stack on localhost and add both before exposing document data on a public network.

## Real document workflow check

On 16 September 2026, an [angled sample document JPEG](https://github.com/pcaswathiii/document-scanner-ocr/blob/main/sample_test_image.jpg) was downloaded to the Git-ignored `.local-data` directory and uploaded to `POST /api/documents`. This was a live request through ASP.NET Core, FastAPI, OpenCV, and `smart_document_db`, not a mocked test response.

- The API returned HTTP 201 and document ID `c92bcd83-1c64-4819-a6ff-bae5bcdc537d` with `Completed`, `documentDetected=true`, `blurDetected=false`, quality score `76.6`, processing time `46 ms`, and rotation `-5.7°`.
- Backend logs show the upload, `POST http://localhost:8000/analyze` returning HTTP 200, and the completed API response. FastAPI logs show `POST /analyze` returning HTTP 200.
- The original image retrieved from the API exactly matches the uploaded JPEG by SHA-256. The processed JPEG was retrieved separately and visually inspected: the page is cropped and straightened, changing from `1500×1800` to `1308×1574` pixels. Dark background pixels fell from `28.8%` to `2.0%`.
- The processed JPEG's measured Laplacian variance is `840.33`, above the configured blur threshold of `100`, consistent with `blurDetected=false`.
- Direct PostgreSQL querying found the completed row, matching metadata, and both stored image paths. `GET /api/documents/{id}` and the history list returned the same record. The PostgreSQL file log had no new errors during this run; it does not log individual successful statements by default.

The record and images were retained in the local database and upload directory so they can be inspected in the dashboard. The downloaded sample and verification copies remain under Git-ignored `.local-data`.

A second run used a [photographed receipt on a textured surface](https://github.com/charlsefrancis/mobile-document-scanner/blob/master/images/receipt.jpg). The API returned HTTP 201 for record `59209797-2262-49d1-a244-0b303f212be5`, with `documentDetected=true`, `blurDetected=false`, quality score `60.1`, and processing time `92 ms`. The retrieved original has the same SHA-256 hash as the uploaded `3264×2448` JPEG. The processed `1729×1183` JPEG was visually inspected and shows the receipt cropped and perspective-corrected; its Laplacian variance is `223.95`. Backend and FastAPI logs show the live `/analyze` request and HTTP 200 response, and direct SQL plus `GET /api/documents/{id}` confirm the completed row. The receipt text remains sideways because the current pipeline does not infer a 90-degree text orientation.

## Native Windows runtime

The verified development stack runs directly on Windows at ports `5173`, `5000`, `8000`, and `5432`. PostgreSQL 16.15 is stored under the ignored `.tools` directory, and its data is stored under `.local-data`. The local start scripts load secrets from the ignored `.env` file. Docker is not required for this workflow.

## Browser workflow check

On 17 September 2026, the running React app at `http://localhost:5173` was exercised in the browser against the local ASP.NET Core, FastAPI/OpenCV, and PostgreSQL services:

| Scenario | Observed result |
|---|---|
| Normal receipt JPEG | Preview appeared before submission; the button showed its processing state; original and corrected images loaded; the result showed boundary detected, sharp focus, quality 60/100, rotation 4.3°, and processing time 134 ms. |
| Heavily blurred receipt JPEG | The result showed blur detected, quality 36/100, and no reliable document boundary. The processed view retained the original framing. |
| Unsupported text file | The UI showed “Choose a JPEG or PNG image” and kept Analyze disabled. A direct multipart request to the backend returned HTTP 400 with an `application/problem+json` response. No record was added. |
| Non-document fruit photo | Processing completed with `documentDetected=false`, sharp focus, quality 52/100, and original framing retained. |
| Processing history | A fresh browser tab loaded the same three new records and previous records found directly in `smart_document_db`. |
| Delete browser-created receipt record | The inline confirmation led to HTTP DELETE 204. The row disappeared from the UI; GET by ID returned 404, and a direct PostgreSQL count for that ID returned zero. |

FastAPI access logs recorded three successful `POST /analyze` calls. Backend logs recorded the history and image GET requests, successful cross-origin DELETE request, and successful computer-vision calls. The browser console had no warnings or errors. The PostgreSQL log showed the database accepting connections and no new errors during this check; PostgreSQL does not log individual successful statements by default. Final local checks returned HTTP 200 for the frontend, backend Swagger, and FastAPI docs; `/health/ready` reported both the database and computer-vision service connected. The React/TypeScript production build passed after the UI fixes.

The browser run exposed two interface issues that were corrected: selecting a file now shows a ready preview instead of a premature processing animation, and a narrow result card now gives the quality metric its own row. Deletion now uses an accessible inline confirmation instead of a blocking browser dialog. The PostgreSQL start script also now uses `pg_isready` for its initial connection probe so an expected connection refusal does not abort startup under PowerShell's stop-on-error setting.

## Frontend presentation check

On 17 September 2026, the React interface was refined for a portfolio presentation: an explicit application title, simpler header, more legible labels, clear upload and processing states, action-specific success messages, file removal, and history cards at narrow widths. The existing backend API and processing architecture were unchanged.

The browser check selected and removed a JPEG preview, rejected a text file with Analyze disabled, uploaded a receipt through the live API, displayed both loaded images and all analysis metrics, refreshed history, and deleted the browser-created test record with an inline confirmation. A second small upload verified the final analysis and deletion message wording. The temporary records from this presentation check were deleted; the earlier sample records remain for demonstration. A narrow viewport check found and corrected horizontal overflow, and verified stacked image comparison and history cards. The browser console showed no warnings or errors. `npm run build` passed, and the frontend, backend readiness endpoint, and FastAPI docs remained available locally.
