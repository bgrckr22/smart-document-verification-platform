import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiError, apiDocsUrl, checkReadiness, deleteDocument, listDocuments, uploadDocument } from "./api";
import { AnalysisPanel } from "./components/AnalysisPanel";
import { BrandIcon, CheckIcon, ScanIcon } from "./components/Icons";
import { HistoryTable } from "./components/HistoryTable";
import { UploadPanel } from "./components/UploadPanel";
import type { DocumentRecord } from "./types";

interface Notice {
  title: string;
  message: string;
}

export default function App() {
  const [documents, setDocuments] = useState<DocumentRecord[]>([]);
  const [totalDocuments, setTotalDocuments] = useState(0);
  const [selectedDocument, setSelectedDocument] = useState<DocumentRecord | null>(null);
  const [localPreviewUrl, setLocalPreviewUrl] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [isPipelineReady, setIsPipelineReady] = useState(false);

  const loadHistory = useCallback(async () => {
    setIsLoading(true);
    const [historyResult, readinessResult] = await Promise.allSettled([listDocuments(), checkReadiness()]);
    setIsPipelineReady(readinessResult.status === "fulfilled");
    if (historyResult.status === "fulfilled") {
      const response = historyResult.value;
      setDocuments(response.items);
      setTotalDocuments(response.total);
      setSelectedDocument((current) => {
        if (!current) return response.items[0] ?? null;
        return response.items.find((item) => item.id === current.id) ?? response.items[0] ?? null;
      });
      setError(readinessResult.status === "rejected" ? errorMessage(readinessResult.reason) : null);
    } else {
      setError(errorMessage(historyResult.reason));
    }
    setIsLoading(false);
    setHasLoaded(true);
  }, []);

  async function loadMoreHistory() {
    if (isLoading || isLoadingMore || documents.length >= totalDocuments) return;
    setIsLoadingMore(true);
    try {
      const response = await listDocuments(documents.length);
      setDocuments((current) => [
        ...current,
        ...response.items.filter((item) => !current.some((existing) => existing.id === item.id)),
      ]);
      setTotalDocuments(response.total);
      setError(null);
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setIsLoadingMore(false);
    }
  }

  useEffect(() => {
    void loadHistory();
  }, [loadHistory]);

  useEffect(() => () => {
    if (localPreviewUrl) URL.revokeObjectURL(localPreviewUrl);
  }, [localPreviewUrl]);

  function previewFile(file: File | null) {
    setNotice(null);
    if (!file) {
      setLocalPreviewUrl(null);
      return;
    }
    const nextUrl = URL.createObjectURL(file);
    setLocalPreviewUrl(nextUrl);
    setSelectedDocument(null);
    setError(null);
  }

  async function handleUpload(file: File) {
    setIsUploading(true);
    setError(null);
    setNotice(null);
    try {
      const document = await uploadDocument(file);
      setLocalPreviewUrl(null);
      setSelectedDocument(document);
      setDocuments((current) => [document, ...current.filter((item) => item.id !== document.id)]);
      setTotalDocuments((current) => current + 1);
      setIsPipelineReady(true);
      if (document.status === "Completed") {
        setNotice({
          title: "Analysis complete",
          message: `${document.originalFileName} was analyzed and saved to processing history.`,
        });
      } else {
        setError(document.errorMessage ?? "The image was saved, but analysis did not complete. Try another image.");
      }
      return true;
    } catch (caught) {
      const message = errorMessage(caught);
      await loadHistory();
      setError(message);
      setNotice(null);
      return false;
    } finally {
      setIsUploading(false);
    }
  }

  async function handleDelete(document: DocumentRecord) {
    setDeletingId(document.id);
    setError(null);
    setNotice(null);
    try {
      await deleteDocument(document.id);
      setDocuments((current) => current.filter((item) => item.id !== document.id));
      setTotalDocuments((current) => Math.max(0, current - 1));
      setSelectedDocument((current) => current?.id === document.id
        ? documents.find((item) => item.id !== document.id) ?? null
        : current);
      setNotice({
        title: "Record deleted",
        message: `${document.originalFileName} was removed from processing history.`,
      });
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setDeletingId(null);
    }
  }

  const completedCount = useMemo(
    () => documents.filter((document) => document.status === "Completed").length,
    [documents],
  );

  return (
    <div className="app-shell">
      <header className="site-header">
        <nav className="nav-bar" aria-label="Primary navigation">
          <a className="brand" href="#top" aria-label="Smart Document Verification home">
            <span className="brand-mark"><BrandIcon width={28} height={28} /></span>
            <span>Smart<span>Verify</span></span>
          </a>
          <div className="nav-meta">
            <span className={`service-status ${!isPipelineReady ? "has-error" : ""}`}>
              <i /> {!hasLoaded && isLoading ? "Connecting" : isPipelineReady ? "System ready" : "Processing unavailable"}
            </span>
            <a href={apiDocsUrl} target="_blank" rel="noreferrer">API docs</a>
          </div>
        </nav>

        <div className="hero" id="top">
          <div className="hero-copy">
            <span className="hero-kicker"><ScanIcon /> Computer vision workspace</span>
            <h1>Smart Document <span>Verification Platform</span></h1>
            <p>Upload a document photo to straighten its perspective and check focus, boundaries, and image quality. Review every result in one place.</p>
          </div>
          <div className="hero-stats" aria-label="Workspace summary">
            <div><span>Total files</span><strong>{totalDocuments}</strong></div>
            <div><span>Completed shown</span><strong>{completedCount}</strong></div>
            <div><span>Pipeline</span><strong className={`pipeline-label ${!isPipelineReady ? "has-error" : ""}`}><CheckIcon /> {isPipelineReady ? "Online" : "Check"}</strong></div>
          </div>
        </div>
      </header>

      <main>
        {error && (
          <div className="feedback-banner error-banner" role="alert">
            <div><strong>Unable to complete the request</strong><span>{error}</span></div>
            <button type="button" onClick={() => setError(null)} aria-label="Dismiss error">×</button>
          </div>
        )}
        {notice && (
          <div className="feedback-banner success-banner" role="status">
            <CheckIcon aria-hidden="true" />
            <div><strong>{notice.title}</strong><span>{notice.message}</span></div>
            <button type="button" onClick={() => setNotice(null)} aria-label="Dismiss message">×</button>
          </div>
        )}

        <div className="workspace-grid">
          <UploadPanel isUploading={isUploading} onUpload={handleUpload} onPreview={previewFile} />
          <AnalysisPanel document={selectedDocument} localPreviewUrl={localPreviewUrl} isUploading={isUploading} />
        </div>

        <HistoryTable
          documents={documents}
          total={totalDocuments}
          selectedId={selectedDocument?.id ?? null}
          isLoading={isLoading}
          isLoadingMore={isLoadingMore}
          deletingId={deletingId}
          onSelect={(document) => {
            setSelectedDocument(document);
            setLocalPreviewUrl(null);
            setNotice(null);
            window.scrollTo({ top: 0, behavior: "smooth" });
          }}
          onDelete={handleDelete}
          onRefresh={() => void loadHistory()}
          onLoadMore={() => void loadMoreHistory()}
        />
      </main>

      <footer>
        <span>Smart Document Verification Platform</span>
        <span>ASP.NET Core · FastAPI · OpenCV · PostgreSQL</span>
      </footer>
    </div>
  );
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.traceId ? `${error.message} Reference: ${error.traceId}` : error.message;
  }
  if (error instanceof TypeError) {
    return "The API could not be reached. Check that the local services are running.";
  }
  return error instanceof Error ? error.message : "An unexpected error occurred.";
}
