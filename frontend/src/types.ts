export type ProcessingStatus = "Processing" | "Completed" | "Failed";

export interface DocumentRecord {
  id: string;
  originalFileName: string;
  uploadedAt: string;
  status: ProcessingStatus;
  qualityScore: number | null;
  blurDetected: boolean | null;
  documentDetected: boolean | null;
  processingTimeMs: number | null;
  rotationDegrees: number | null;
  orientation: string | null;
  errorMessage: string | null;
  originalImageUrl: string;
  processedImageUrl: string | null;
}

export interface PagedResponse<T> {
  items: T[];
  total: number;
  skip: number;
  take: number;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
}
