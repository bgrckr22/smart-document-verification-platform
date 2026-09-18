from __future__ import annotations

import base64
import math
import os
import time
from dataclasses import dataclass

from app.config import positive_int_environment

MAX_IMAGE_PIXELS = positive_int_environment("MAX_IMAGE_PIXELS", 40_000_000)
os.environ["OPENCV_IO_MAX_IMAGE_PIXELS"] = str(MAX_IMAGE_PIXELS)

import cv2
import numpy as np
from numpy.typing import NDArray


MAX_DETECTION_DIMENSION = 1600
MIN_DOCUMENT_AREA_RATIO = 0.20
BLUR_VARIANCE_THRESHOLD = 100.0


class ImageProcessingError(ValueError):
    """Raised when uploaded bytes cannot be processed as an image."""


@dataclass(frozen=True)
class DocumentBoundary:
    points: NDArray[np.float32] | None
    detected: bool
    rotation_degrees: float | None


@dataclass(frozen=True)
class ProcessingResult:
    quality_score: float
    blur_detected: bool
    document_detected: bool
    processing_time_ms: int
    rotation_degrees: float | None
    orientation: str
    laplacian_variance: float
    processed_image: NDArray[np.uint8]


def decode_image(image_bytes: bytes) -> NDArray[np.uint8]:
    if not image_bytes:
        raise ImageProcessingError("The uploaded file is empty.")

    encoded = np.frombuffer(image_bytes, dtype=np.uint8)
    image = cv2.imdecode(encoded, cv2.IMREAD_COLOR)
    if image is None:
        raise ImageProcessingError("The uploaded file is not a valid JPEG or PNG image.")
    height, width = image.shape[:2]
    if height * width > MAX_IMAGE_PIXELS:
        raise ImageProcessingError(
            f"The decoded image exceeds the {MAX_IMAGE_PIXELS:,} pixel limit."
        )
    return image


def preprocess_for_edges(image: NDArray[np.uint8]) -> tuple[NDArray[np.uint8], NDArray[np.uint8]]:
    """Return the grayscale image and a cleaned Canny edge map."""
    grayscale = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(grayscale, (5, 5), 0)
    edges = cv2.Canny(blurred, 75, 200)
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (5, 5))
    closed_edges = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, kernel)
    return grayscale, closed_edges


def detect_document_boundary(image: NDArray[np.uint8]) -> DocumentBoundary:
    working_image, scale_to_original = _resize_for_detection(image)
    _, edges = preprocess_for_edges(working_image)
    contours, _ = cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    minimum_area = working_image.shape[0] * working_image.shape[1] * MIN_DOCUMENT_AREA_RATIO

    for contour in sorted(contours, key=cv2.contourArea, reverse=True)[:15]:
        if cv2.contourArea(contour) < minimum_area:
            break

        perimeter = cv2.arcLength(contour, True)
        polygon = cv2.approxPolyDP(contour, 0.02 * perimeter, True)
        if len(polygon) == 4 and cv2.isContourConvex(polygon):
            points = polygon.reshape(4, 2).astype(np.float32) * scale_to_original
            ordered = order_points(points)
            return DocumentBoundary(
                points=ordered,
                detected=True,
                rotation_degrees=_rotation_from_top_edge(ordered),
            )

    return DocumentBoundary(points=None, detected=False, rotation_degrees=None)


def order_points(points: NDArray[np.float32]) -> NDArray[np.float32]:
    """Order quadrilateral points as top-left, top-right, bottom-right, bottom-left."""
    center = points.mean(axis=0)
    angles = np.arctan2(points[:, 1] - center[1], points[:, 0] - center[0])
    clockwise = points[np.argsort(angles)]
    top_left_index = int(np.argmin(clockwise.sum(axis=1)))
    return np.roll(clockwise, -top_left_index, axis=0).astype(np.float32)


def apply_perspective_correction(
    image: NDArray[np.uint8], points: NDArray[np.float32]
) -> NDArray[np.uint8]:
    top_left, top_right, bottom_right, bottom_left = order_points(points)

    width_top = np.linalg.norm(top_right - top_left)
    width_bottom = np.linalg.norm(bottom_right - bottom_left)
    height_right = np.linalg.norm(bottom_right - top_right)
    height_left = np.linalg.norm(bottom_left - top_left)
    output_width = max(1, int(round(max(width_top, width_bottom))))
    output_height = max(1, int(round(max(height_right, height_left))))
    if output_width * output_height > MAX_IMAGE_PIXELS:
        raise ImageProcessingError(
            f"The corrected image exceeds the {MAX_IMAGE_PIXELS:,} pixel limit."
        )

    destination = np.array(
        [
            [0, 0],
            [output_width - 1, 0],
            [output_width - 1, output_height - 1],
            [0, output_height - 1],
        ],
        dtype=np.float32,
    )
    transform = cv2.getPerspectiveTransform(
        np.array([top_left, top_right, bottom_right, bottom_left], dtype=np.float32),
        destination,
    )
    return cv2.warpPerspective(image, transform, (output_width, output_height))


def calculate_laplacian_variance(image: NDArray[np.uint8]) -> float:
    grayscale = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    return float(cv2.Laplacian(grayscale, cv2.CV_64F).var())


def calculate_quality_score(
    image: NDArray[np.uint8], laplacian_variance: float, document_detected: bool
) -> float:
    """Combine explainable sharpness, exposure, contrast, and boundary scores."""
    grayscale = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    brightness = float(grayscale.mean())
    contrast = float(grayscale.std())

    sharpness_score = float(np.clip(laplacian_variance / 5.0, 0, 100))
    exposure_score = float(np.clip(100 - abs(brightness - 127.5) / 1.275, 0, 100))
    contrast_score = float(np.clip(contrast / 0.64, 0, 100))
    boundary_score = 100.0 if document_detected else 40.0

    score = (
        sharpness_score * 0.45
        + exposure_score * 0.20
        + contrast_score * 0.20
        + boundary_score * 0.15
    )
    return round(float(np.clip(score, 0, 100)), 1)


def analyze_image(image_bytes: bytes) -> ProcessingResult:
    started_at = time.perf_counter()
    original = decode_image(image_bytes)
    boundary = detect_document_boundary(original)
    processed = (
        apply_perspective_correction(original, boundary.points)
        if boundary.points is not None
        else original.copy()
    )

    laplacian_variance = calculate_laplacian_variance(processed)
    orientation = _orientation_from_shape(processed.shape[1], processed.shape[0])
    quality_score = calculate_quality_score(
        processed, laplacian_variance, boundary.detected
    )
    elapsed_ms = max(0, round((time.perf_counter() - started_at) * 1000))

    return ProcessingResult(
        quality_score=quality_score,
        blur_detected=laplacian_variance < BLUR_VARIANCE_THRESHOLD,
        document_detected=boundary.detected,
        processing_time_ms=elapsed_ms,
        rotation_degrees=boundary.rotation_degrees,
        orientation=orientation,
        laplacian_variance=round(laplacian_variance, 2),
        processed_image=processed,
    )


def encode_processed_image(image: NDArray[np.uint8]) -> str:
    success, encoded = cv2.imencode(
        ".jpg", image, [int(cv2.IMWRITE_JPEG_QUALITY), 92]
    )
    if not success:
        raise ImageProcessingError("OpenCV could not encode the processed image.")
    return base64.b64encode(encoded.tobytes()).decode("ascii")


def _resize_for_detection(
    image: NDArray[np.uint8],
) -> tuple[NDArray[np.uint8], float]:
    height, width = image.shape[:2]
    largest_dimension = max(height, width)
    if largest_dimension <= MAX_DETECTION_DIMENSION:
        return image.copy(), 1.0

    working_scale = MAX_DETECTION_DIMENSION / largest_dimension
    resized = cv2.resize(
        image,
        (max(1, round(width * working_scale)), max(1, round(height * working_scale))),
        interpolation=cv2.INTER_AREA,
    )
    return resized, 1.0 / working_scale


def _rotation_from_top_edge(points: NDArray[np.float32]) -> float:
    top_left, top_right = points[0], points[1]
    angle = math.degrees(
        math.atan2(float(top_right[1] - top_left[1]), float(top_right[0] - top_left[0]))
    )
    return round(-angle, 1)


def _orientation_from_shape(width: int, height: int) -> str:
    ratio = width / height
    if 0.95 <= ratio <= 1.05:
        return "square"
    return "landscape" if ratio > 1 else "portrait"
