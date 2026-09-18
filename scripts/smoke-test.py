"""Exercise the running application using only Python's standard library."""

from __future__ import annotations

import binascii
import json
import os
import struct
import time
import urllib.error
import urllib.request
import uuid
import zlib


BACKEND_URL = os.getenv("SMOKE_BACKEND_URL", "http://localhost:5000").rstrip("/")
FRONTEND_URL = os.getenv("SMOKE_FRONTEND_URL", "http://localhost:5173").rstrip("/")
PUBLIC_API_URL = os.getenv("SMOKE_PUBLIC_API_URL", FRONTEND_URL).rstrip("/")
KEEP_DOCUMENT = os.getenv("SMOKE_KEEP_DOCUMENT", "").lower() in {"1", "true", "yes"}


def main() -> None:
    wait_until_ready(f"{BACKEND_URL}/health/ready")
    wait_until_ready(FRONTEND_URL)

    source_image = create_sample_png()
    upload = upload_document(source_image)
    document_id = upload["id"]

    assert upload["status"] == "Completed", upload
    assert fetch_bytes(f"{PUBLIC_API_URL}{upload['originalImageUrl']}") == source_image
    assert fetch_bytes(f"{PUBLIC_API_URL}{upload['processedImageUrl']}").startswith(
        b"\xff\xd8\xff"
    )

    history = fetch_json(f"{PUBLIC_API_URL}/api/documents")
    assert any(item["id"] == document_id for item in history["items"]), history

    if not KEEP_DOCUMENT:
        request = urllib.request.Request(
            f"{PUBLIC_API_URL}/api/documents/{document_id}", method="DELETE"
        )
        with urllib.request.urlopen(request, timeout=10) as response:
            assert response.status == 204

    retention = "retained" if KEEP_DOCUMENT else "deleted"
    print(f"Smoke test passed for document {document_id} ({retention}).")
    print(f"SMOKE_DOCUMENT_ID={document_id}")


def wait_until_ready(url: str, timeout_seconds: int = 240) -> None:
    deadline = time.monotonic() + timeout_seconds
    last_error: Exception | None = None
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=5) as response:
                if response.status == 200:
                    return
        except (OSError, urllib.error.HTTPError) as exception:
            last_error = exception
        time.sleep(2)
    raise TimeoutError(f"{url} did not become ready: {last_error}")


def upload_document(image: bytes) -> dict[str, object]:
    boundary = f"----smart-document-{uuid.uuid4().hex}"
    body = (
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="file"; filename="smoke-document.png"\r\n'
        "Content-Type: image/png\r\n\r\n"
    ).encode("ascii") + image + f"\r\n--{boundary}--\r\n".encode("ascii")
    request = urllib.request.Request(
        f"{PUBLIC_API_URL}/api/documents",
        data=body,
        method="POST",
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )
    with urllib.request.urlopen(request, timeout=90) as response:
        assert response.status == 201
        return json.load(response)


def fetch_json(url: str) -> dict[str, object]:
    with urllib.request.urlopen(url, timeout=10) as response:
        assert response.status == 200
        return json.load(response)


def fetch_bytes(url: str) -> bytes:
    with urllib.request.urlopen(url, timeout=10) as response:
        assert response.status == 200
        return response.read()


def create_sample_png(width: int = 320, height: int = 240) -> bytes:
    rows = []
    for y in range(height):
        row = bytearray([0])
        for x in range(width):
            inside_document = 35 < x < width - 35 and 25 < y < height - 25
            value = 240 if inside_document else 25
            row.extend((value, value, value))
        rows.append(bytes(row))

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", header)
        + png_chunk(b"IDAT", zlib.compress(b"".join(rows)))
        + png_chunk(b"IEND", b"")
    )


def png_chunk(kind: bytes, data: bytes) -> bytes:
    checksum = binascii.crc32(kind + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", checksum)


if __name__ == "__main__":
    main()
