import cv2
import numpy as np
import pytest

from app import image_processor
from app.image_processor import (
    ImageProcessingError,
    analyze_image,
    apply_perspective_correction,
    decode_image,
    detect_document_boundary,
    order_points,
)


def create_document_image() -> np.ndarray:
    image = np.full((900, 1100, 3), 28, dtype=np.uint8)
    document = np.array([[170, 110], [930, 150], [880, 790], [120, 740]], np.int32)
    cv2.fillConvexPoly(image, document, (242, 242, 238))
    for offset in range(220, 680, 70):
        cv2.line(image, (250, offset), (790, offset + 25), (45, 45, 45), 8)
    return image


def encode_jpeg(image: np.ndarray) -> bytes:
    success, encoded = cv2.imencode(".jpg", image)
    assert success
    return encoded.tobytes()


def test_detects_large_document_boundary() -> None:
    boundary = detect_document_boundary(create_document_image())

    assert boundary.detected is True
    assert boundary.points is not None
    assert boundary.points.shape == (4, 2)


def test_analyze_corrects_perspective_and_returns_metrics() -> None:
    result = analyze_image(encode_jpeg(create_document_image()))

    assert result.document_detected is True
    assert result.processed_image.shape[0] > 500
    assert result.processed_image.shape[1] > 600
    assert 0 <= result.quality_score <= 100
    assert result.orientation == "landscape"
    assert result.rotation_degrees is not None


def test_blur_detection_distinguishes_blurred_image() -> None:
    sharp_image = create_document_image()
    blurred_image = cv2.GaussianBlur(sharp_image, (51, 51), 0)

    sharp_result = analyze_image(encode_jpeg(sharp_image))
    blurred_result = analyze_image(encode_jpeg(blurred_image))

    assert sharp_result.laplacian_variance > blurred_result.laplacian_variance
    assert blurred_result.blur_detected is True


def test_order_points_returns_clockwise_document_corners() -> None:
    unordered = np.array([[90, 80], [10, 10], [15, 85], [100, 15]], dtype=np.float32)

    ordered = order_points(unordered)

    np.testing.assert_array_equal(ordered[0], [10, 10])
    np.testing.assert_array_equal(ordered[1], [100, 15])
    np.testing.assert_array_equal(ordered[2], [90, 80])
    np.testing.assert_array_equal(ordered[3], [15, 85])


def test_order_points_keeps_all_corners_of_a_diamond() -> None:
    diamond = np.array([[50, 0], [100, 50], [50, 100], [0, 50]], dtype=np.float32)

    ordered = order_points(diamond[[2, 0, 3, 1]])

    assert len({tuple(point) for point in ordered}) == 4
    np.testing.assert_array_equal(ordered[0], [50, 0])


def test_perspective_correction_rejects_oversized_output(monkeypatch) -> None:
    monkeypatch.setattr(image_processor, "MAX_IMAGE_PIXELS", 100)
    image = np.zeros((20, 20, 3), dtype=np.uint8)
    points = np.array([[0, 0], [19, 0], [19, 19], [0, 19]], dtype=np.float32)

    with pytest.raises(ImageProcessingError, match="corrected image"):
        apply_perspective_correction(image, points)


def test_decode_rejects_non_image_bytes() -> None:
    try:
        decode_image(b"this is not an image")
    except ImageProcessingError as exception:
        assert "valid JPEG or PNG" in str(exception)
    else:
        raise AssertionError("Expected ImageProcessingError")


def test_decode_rejects_image_above_pixel_limit(monkeypatch) -> None:
    image = np.zeros((20, 20, 3), dtype=np.uint8)
    monkeypatch.setattr(image_processor, "MAX_IMAGE_PIXELS", 100)

    with pytest.raises(ImageProcessingError, match="pixel limit"):
        decode_image(encode_jpeg(image))
