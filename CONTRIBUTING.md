# Contributing

Thanks for helping improve Smart Document Verification Platform.

## Local workflow

1. Create a focused branch from `main`.
2. Copy `.env.example` to `.env` and use development-only credentials.
3. Run the relevant service tests before opening a pull request.
4. Run the native smoke test when a change affects service integration. Docker Compose verification is optional unless the change touches container files.
5. Keep pull requests small and explain the user-visible behavior they change.

## Code conventions

- Prefer clear names and small functions over explanatory comments.
- Keep HTTP contracts in DTO/schema types rather than returning database entities.
- Add a migration for every database schema change.
- Keep the CV service deterministic. Do not describe heuristics as machine learning.
- Never commit uploaded documents, credentials, build output, or local environment files.

## Verification commands

```bash
dotnet test backend/SmartDocumentPlatform.sln
python -m pytest tests/cv-service
npm --prefix frontend run build
python scripts/smoke-test.py
```

Use `docker compose config` as an additional check when changing Dockerfiles or `docker-compose.yml`.
