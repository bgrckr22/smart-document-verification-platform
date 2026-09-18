from typing import Literal

from pydantic import BaseModel, Field


class AnalysisResponse(BaseModel):
    quality_score: float = Field(ge=0, le=100)
    blur_detected: bool
    document_detected: bool
    processing_time_ms: int = Field(ge=0)
    rotation_degrees: float | None = None
    orientation: Literal["portrait", "landscape", "square"]
    laplacian_variance: float = Field(ge=0)
    processed_image_base64: str
    processed_content_type: Literal["image/jpeg"] = "image/jpeg"
    processed_width: int = Field(gt=0)
    processed_height: int = Field(gt=0)


class HealthResponse(BaseModel):
    status: Literal["healthy"]
    service: Literal["cv-service"]
