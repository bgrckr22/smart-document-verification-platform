import { useRef, useState, type ChangeEvent, type DragEvent } from "react";
import { FileIcon, UploadIcon } from "./Icons";

const MAX_FILE_BYTES = 10 * 1024 * 1024;
const ACCEPTED_TYPES = new Set(["image/jpeg", "image/png"]);
const ACCEPTED_EXTENSIONS = /\.(jpe?g|png)$/i;

interface UploadPanelProps {
  isUploading: boolean;
  onUpload: (file: File) => Promise<boolean>;
  onPreview: (file: File | null) => void;
}

export function UploadPanel({ isUploading, onUpload, onPreview }: UploadPanelProps) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const dragDepth = useRef(0);

  function chooseFile(file: File | undefined) {
    if (!file || isUploading) return;
    if (!ACCEPTED_TYPES.has(file.type) || !ACCEPTED_EXTENSIONS.test(file.name)) {
      setSelectedFile(null);
      onPreview(null);
      setValidationError("Choose a JPEG or PNG image.");
      return;
    }
    if (file.size === 0 || file.size > MAX_FILE_BYTES) {
      setSelectedFile(null);
      onPreview(null);
      setValidationError("The image must be non-empty and 10 MB or smaller.");
      return;
    }
    setValidationError(null);
    setSelectedFile(file);
    onPreview(file);
  }

  function handleInput(event: ChangeEvent<HTMLInputElement>) {
    chooseFile(event.target.files?.[0]);
    event.target.value = "";
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    dragDepth.current = 0;
    setIsDragging(false);
    chooseFile(event.dataTransfer.files?.[0]);
  }

  function removeFile() {
    setSelectedFile(null);
    setValidationError(null);
    onPreview(null);
  }

  async function submit() {
    if (!selectedFile || isUploading) return;
    if (await onUpload(selectedFile)) {
      setSelectedFile(null);
    }
  }

  return (
    <section className="card upload-card" aria-labelledby="upload-title" aria-busy={isUploading}>
      <div className="section-heading">
        <div>
          <span className="eyebrow">New analysis</span>
          <h2 id="upload-title">Upload a document</h2>
        </div>
        <span className="step-badge">01</span>
      </div>

      <div
        className={`drop-zone ${isDragging ? "is-dragging" : ""}`}
        onDragEnter={(event) => {
          event.preventDefault();
          dragDepth.current += 1;
          setIsDragging(true);
        }}
        onDragOver={(event) => event.preventDefault()}
        onDragLeave={(event) => {
          event.preventDefault();
          dragDepth.current = Math.max(0, dragDepth.current - 1);
          if (dragDepth.current === 0) setIsDragging(false);
        }}
        onDrop={handleDrop}
      >
        <div className="upload-icon-wrap"><UploadIcon width={28} height={28} /></div>
        <h3>Drop your document here</h3>
        <p>or select an image from your computer</p>
        <button className="button button-secondary" type="button" disabled={isUploading} onClick={() => inputRef.current?.click()}>
          Browse files
        </button>
        <input
          ref={inputRef}
          className="sr-only"
          type="file"
          accept="image/jpeg,image/png"
          aria-label="Choose a JPEG or PNG image"
          aria-describedby="upload-help"
          disabled={isUploading}
          onChange={handleInput}
        />
        <span className="file-hint" id="upload-help">JPEG or PNG · maximum 10 MB</span>
      </div>

      {validationError && <p className="inline-error" role="alert">{validationError}</p>}

      {selectedFile && (
        <div className="selected-file" role="status">
          <div className="selected-file-icon"><FileIcon /></div>
          <div className="selected-file-copy">
            <strong title={selectedFile.name}>{selectedFile.name}</strong>
            <span>{formatBytes(selectedFile.size)}</span>
          </div>
          <span className="ready-label">Ready</span>
          <button className="remove-file" type="button" disabled={isUploading} onClick={removeFile} aria-label="Remove selected file">×</button>
        </div>
      )}

      <button
        className="button button-primary upload-button"
        type="button"
        disabled={!selectedFile || isUploading}
        onClick={submit}
      >
        {isUploading ? <><span className="spinner" aria-hidden="true" /> Analyzing document…</> : <><UploadIcon /> Analyze document</>}
      </button>

      <div className="privacy-note">
        <span className="privacy-dot" />
        Files stay inside your local environment.
      </div>
    </section>
  );
}

function formatBytes(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
