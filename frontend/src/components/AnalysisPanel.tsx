import type { CSSProperties } from "react";
import { resolveImageUrl } from "../api";
import type { DocumentRecord } from "../types";
import { ImageIcon, ScanIcon } from "./Icons";

interface AnalysisPanelProps {
  document: DocumentRecord | null;
  localPreviewUrl: string | null;
  isUploading: boolean;
}

export function AnalysisPanel({ document, localPreviewUrl, isUploading }: AnalysisPanelProps) {
  if (!document && !localPreviewUrl) {
    return (
      <section className="card analysis-card analysis-empty" aria-label="Analysis result">
        <div className="empty-scanner">
          <ScanIcon width={36} height={36} />
          <span />
        </div>
        <h2>Your analysis will appear here</h2>
        <p>Select a clear photo with the full document visible for the best boundary detection.</p>
        <div className="empty-features">
          <span>Boundary detection</span>
          <span>Perspective correction</span>
          <span>Quality checks</span>
        </div>
      </section>
    );
  }

  if (localPreviewUrl && !isUploading) {
    return (
      <section className="card analysis-card processing-view" aria-label="Selected image preview">
        <div className="processing-preview">
          <img src={localPreviewUrl} alt="Selected document preview" />
        </div>
        <div className="processing-copy">
          <span className="eyebrow">Selected image</span>
          <h2>Ready to analyze</h2>
          <p>Review the image, then choose Analyze document to check its boundary, focus, and quality.</p>
        </div>
      </section>
    );
  }

  if (isUploading || !document) {
    return (
      <section className="card analysis-card processing-view" aria-live="polite" aria-busy="true">
        <div className="processing-preview">
          {localPreviewUrl && <img src={localPreviewUrl} alt="Selected document preview" />}
          <div className="scan-line" />
        </div>
        <div className="processing-copy">
          <span className="eyebrow">OpenCV pipeline</span>
          <h2>Inspecting your document</h2>
          <p>Finding edges, evaluating focus, and correcting the document perspective.</p>
          <div className="processing-steps" role="status">
            <span><span className="spinner spinner-dark" aria-hidden="true" /> Processing image with OpenCV…</span>
          </div>
        </div>
      </section>
    );
  }

  const originalUrl = resolveImageUrl(document.originalImageUrl);

  if (document.status === "Processing") {
    return (
      <section className="card analysis-card failed-analysis" aria-labelledby="pending-result-title">
        <div className="failed-preview">
          <img src={originalUrl} alt="Original document awaiting analysis" />
        </div>
        <div className="failed-copy">
          <span className="status-chip status-processing"><span /> Processing incomplete</span>
          <h2 id="pending-result-title">Analysis has not finished</h2>
          <p>The original image was saved, but there is no processed result yet.</p>
          <small>Upload the image again to retry analysis.</small>
        </div>
      </section>
    );
  }

  if (document.status === "Failed") {
    return (
      <section className="card analysis-card failed-analysis" aria-labelledby="failed-result-title">
        <div className="failed-preview">
          <img src={originalUrl} alt="Original document that could not be processed" />
        </div>
        <div className="failed-copy">
          <span className="status-chip status-failed"><span /> Analysis failed</span>
          <h2 id="failed-result-title">The original is safely stored</h2>
          <p>{document.errorMessage ?? "The processing service could not analyze this image."}</p>
          <small>Try a clear JPEG or PNG with the complete page visible, then upload it again.</small>
        </div>
      </section>
    );
  }

  const processedUrl = document.processedImageUrl
    ? resolveImageUrl(document.processedImageUrl)
    : null;
  const score = document.qualityScore ?? 0;

  return (
    <section className="card result-card" aria-labelledby="result-title">
      <div className="section-heading result-heading">
        <div>
          <span className="eyebrow">Latest result</span>
          <h2 id="result-title">Analysis complete</h2>
        </div>
        <span className={`status-chip status-${document.status.toLowerCase()}`}>
          <span /> {document.status}
        </span>
      </div>

      <div className="image-comparison">
        <figure>
          <figcaption><span>Original</span><small>{document.originalFileName}</small></figcaption>
          <div className="image-frame"><img src={originalUrl} alt="Original uploaded document" /></div>
        </figure>
        <figure>
          <figcaption><span>Processed</span><small>{document.documentDetected ? "Perspective corrected" : "Original framing retained"}</small></figcaption>
          <div className="image-frame processed-frame">
            {processedUrl ? (
              <img src={processedUrl} alt="Processed document" />
            ) : (
              <div className="image-unavailable"><ImageIcon /> No processed image</div>
            )}
          </div>
        </figure>
      </div>

      <div className="metrics-grid">
        <div className="quality-metric">
          <div
            className="score-ring"
            style={{ "--quality-score": `${score * 3.6}deg` } as CSSProperties}
            aria-label={`Quality score ${Math.round(score)} out of 100`}
          >
            <div><strong>{Math.round(score)}</strong><span>/100</span></div>
          </div>
          <div><span className="metric-label">Quality score</span><strong>{qualityLabel(score)}</strong></div>
        </div>
        <Metric label="Document" value={document.documentDetected ? "Detected" : "Not detected"} positive={document.documentDetected === true} />
        <Metric label="Focus" value={document.blurDetected ? "Blur detected" : "Sharp"} positive={document.blurDetected === false} />
        <Metric label="Orientation" value={capitalize(document.orientation ?? "Unknown")} />
        <Metric label="Rotation" value={document.rotationDegrees == null ? "Not available" : `${document.rotationDegrees.toFixed(1)}°`} />
        <Metric label="Processing time" value={document.processingTimeMs == null ? "—" : `${document.processingTimeMs} ms`} />
      </div>
    </section>
  );
}

function Metric({ label, value, positive }: { label: string; value: string; positive?: boolean }) {
  return (
    <div className="metric">
      <span className="metric-label">{label}</span>
      <strong className={positive === undefined ? "" : positive ? "positive" : "attention"}>
        {positive !== undefined && <span className="metric-dot" />}{value}
      </strong>
    </div>
  );
}

function qualityLabel(score: number): string {
  if (score >= 80) return "Excellent capture";
  if (score >= 60) return "Good capture";
  if (score >= 40) return "Usable capture";
  return "Retake suggested";
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
