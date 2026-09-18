from fastapi import FastAPI, File, HTTPException, UploadFile, status
from starlette.concurrency import run_in_threadpool

from app.body_limit import RequestBodyLimitMiddleware
from app.config import positive_int_environment
from app.image_processor import ImageProcessingError, analyze_image, encode_processed_image
from app.schemas import AnalysisResponse, HealthResponse


ALLOWED_CONTENT_TYPES = {"image/jpeg", "image/png"}
MAX_UPLOAD_BYTES = positive_int_environment("MAX_UPLOAD_BYTES", 10 * 1024 * 1024)
MAX_MULTIPART_OVERHEAD_BYTES = 1024 * 1024

app = FastAPI(
    title="Smart Document Computer Vision Service",
    version="1.0.0",
    description="Deterministic OpenCV document detection and image-quality analysis.",
)
app.add_middleware(
    RequestBodyLimitMiddleware,
    max_body_bytes=MAX_UPLOAD_BYTES + MAX_MULTIPART_OVERHEAD_BYTES,
)


@app.get("/health", response_model=HealthResponse, tags=["system"])
def health() -> HealthResponse:
    return HealthResponse(status="healthy", service="cv-service")


@app.post(
    "/analyze",
    response_model=AnalysisResponse,
    status_code=status.HTTP_200_OK,
    tags=["analysis"],
)
async def analyze(file: UploadFile = File(...)) -> AnalysisResponse:
    try:
        if file.content_type not in ALLOWED_CONTENT_TYPES:
            raise HTTPException(
                status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
                detail="Only JPEG and PNG images are supported.",
            )
        image_bytes = await file.read(MAX_UPLOAD_BYTES + 1)
    finally:
        await file.close()

    if len(image_bytes) > MAX_UPLOAD_BYTES:
        raise HTTPException(
            status_code=status.HTTP_413_CONTENT_TOO_LARGE,
            detail=f"The image exceeds the {MAX_UPLOAD_BYTES} byte limit.",
        )

    signature_matches = (
        image_bytes.startswith(b"\xff\xd8\xff")
        if file.content_type == "image/jpeg"
        else image_bytes.startswith(b"\x89PNG\r\n\x1a\n")
    )
    if image_bytes and not signature_matches:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail="The file content does not match its declared image type.",
        )

    try:
        return await run_in_threadpool(build_analysis_response, image_bytes)
    except ImageProcessingError as exception:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=str(exception),
        ) from exception


def build_analysis_response(image_bytes: bytes) -> AnalysisResponse:
    import cv2

    try:
        result = analyze_image(image_bytes)
        return AnalysisResponse(
            quality_score=result.quality_score,
            blur_detected=result.blur_detected,
            document_detected=result.document_detected,
            processing_time_ms=result.processing_time_ms,
            rotation_degrees=result.rotation_degrees,
            orientation=result.orientation,
            laplacian_variance=result.laplacian_variance,
            processed_image_base64=encode_processed_image(result.processed_image),
            processed_width=result.processed_image.shape[1],
            processed_height=result.processed_image.shape[0],
        )
    except cv2.error as exception:
        raise ImageProcessingError("OpenCV could not process the uploaded image.") from exception
