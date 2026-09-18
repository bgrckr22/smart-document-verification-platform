import asyncio
from io import BytesIO

import cv2
import httpx
import numpy as np
import pytest
from fastapi import HTTPException, UploadFile
from starlette.datastructures import Headers

from app.body_limit import RequestBodyLimitMiddleware
from app.main import analyze, app


async def request(method: str, path: str, **kwargs: object) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.request(method, path, **kwargs)


def test_health_endpoint() -> None:
    response = asyncio.run(request("GET", "/health"))

    assert response.status_code == 200
    assert response.json() == {"status": "healthy", "service": "cv-service"}


def test_analyze_endpoint_returns_processed_image() -> None:
    image = np.full((600, 800, 3), 30, dtype=np.uint8)
    cv2.rectangle(image, (100, 70), (700, 530), (245, 245, 245), -1)
    cv2.putText(image, "DOCUMENT", (220, 300), cv2.FONT_HERSHEY_SIMPLEX, 2, (20, 20, 20), 5)
    success, encoded = cv2.imencode(".png", image)
    assert success

    response = asyncio.run(request(
        "POST",
        "/analyze",
        files={"file": ("document.png", encoded.tobytes(), "image/png")},
    ))

    assert response.status_code == 200
    body = response.json()
    assert body["document_detected"] is True
    assert body["processed_image_base64"]
    assert 0 <= body["quality_score"] <= 100


def test_analyze_rejects_unsupported_media_type() -> None:
    response = asyncio.run(request(
        "POST",
        "/analyze",
        files={"file": ("notes.txt", b"hello", "text/plain")},
    ))

    assert response.status_code == 415
    assert response.json()["detail"] == "Only JPEG and PNG images are supported."


def test_rejected_upload_closes_its_temporary_file() -> None:
    uploaded = UploadFile(
        file=BytesIO(b"not an image"),
        filename="notes.txt",
        headers=Headers({"content-type": "text/plain"}),
    )

    with pytest.raises(HTTPException) as exception:
        asyncio.run(analyze(uploaded))

    assert exception.value.status_code == 415
    assert uploaded.file.closed


def test_analyze_rejects_invalid_image_bytes() -> None:
    response = asyncio.run(request(
        "POST",
        "/analyze",
        files={"file": ("broken.png", b"not an image", "image/png")},
    ))

    assert response.status_code == 422
    assert response.json()["detail"] == "The file content does not match its declared image type."


def test_analyze_rejects_mislabeled_image() -> None:
    image = np.zeros((20, 20, 3), dtype=np.uint8)
    success, encoded = cv2.imencode(".jpg", image)
    assert success

    response = asyncio.run(request(
        "POST",
        "/analyze",
        files={"file": ("document.png", encoded.tobytes(), "image/png")},
    ))

    assert response.status_code == 422
    assert "declared image type" in response.json()["detail"]


def test_body_limit_rejects_request_before_multipart_parsing() -> None:
    limited_app = RequestBodyLimitMiddleware(app, max_body_bytes=100)

    async def send_request() -> httpx.Response:
        transport = httpx.ASGITransport(app=limited_app)
        async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
            return await client.post(
                "/analyze",
                files={"file": ("document.png", b"x" * 101, "image/png")},
            )

    response = asyncio.run(send_request())

    assert response.status_code == 413
    assert "request size limit" in response.json()["detail"]


def test_body_limit_rejects_chunked_request_without_content_length() -> None:
    limited_app = RequestBodyLimitMiddleware(app, max_body_bytes=100)
    boundary = "test-boundary"
    body = (
        b"--test-boundary\r\n"
        b'Content-Disposition: form-data; name="file"; filename="document.png"\r\n'
        b"Content-Type: image/png\r\n\r\n"
        + b"x" * 101
        + b"\r\n--test-boundary--\r\n"
    )

    async def chunks():
        yield body[:50]
        yield body[50:]

    async def send_request() -> httpx.Response:
        transport = httpx.ASGITransport(app=limited_app)
        async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
            return await client.post(
                "/analyze",
                content=chunks(),
                headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
            )

    response = asyncio.run(send_request())

    assert response.status_code == 413


def test_analyze_enforces_configured_byte_limit(monkeypatch) -> None:
    monkeypatch.setattr("app.main.MAX_UPLOAD_BYTES", 8)

    response = asyncio.run(request(
        "POST",
        "/analyze",
        files={"file": ("large.png", b"123456789", "image/png")},
    ))

    assert response.status_code == 413
