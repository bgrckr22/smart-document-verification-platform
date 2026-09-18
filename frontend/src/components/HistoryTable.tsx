import { Fragment, useState } from "react";
import type { DocumentRecord } from "../types";
import { ClockIcon, FileIcon, RefreshIcon, TrashIcon } from "./Icons";

interface HistoryTableProps {
  documents: DocumentRecord[];
  total: number;
  selectedId: string | null;
  isLoading: boolean;
  isLoadingMore: boolean;
  deletingId: string | null;
  onSelect: (document: DocumentRecord) => void;
  onDelete: (document: DocumentRecord) => void;
  onRefresh: () => void;
  onLoadMore: () => void;
}

export function HistoryTable({
  documents,
  total,
  selectedId,
  isLoading,
  isLoadingMore,
  deletingId,
  onSelect,
  onDelete,
  onRefresh,
  onLoadMore,
}: HistoryTableProps) {
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  return (
    <section className="card history-card" aria-labelledby="history-title" aria-busy={isLoading || isLoadingMore}>
      <div className="history-header">
        <div>
          <span className="eyebrow">Saved locally</span>
          <h2 id="history-title">Processing history</h2>
        </div>
        <button className="icon-button refresh-button" type="button" onClick={onRefresh} disabled={isLoading || isLoadingMore} aria-label={isLoading ? "Refreshing history" : "Refresh history"}>
          <RefreshIcon className={isLoading ? "is-spinning" : ""} />
        </button>
      </div>

      {documents.length === 0 && isLoading ? (
        <div className="history-empty" role="status">Loading history…</div>
      ) : documents.length === 0 ? (
        <div className="history-empty">
          <ClockIcon />
          <div><strong>No documents yet</strong><span>Your document analyses will be listed here.</span></div>
        </div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Document</th>
                <th>Uploaded</th>
                <th>Status</th>
                <th>Quality</th>
                <th>Checks</th>
                <th><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {documents.map((document) => (
                <Fragment key={document.id}>
                  <tr className={selectedId === document.id ? "is-selected" : ""}>
                    <td>
                      <button className="document-link" type="button" aria-current={selectedId === document.id ? "true" : undefined} onClick={() => onSelect(document)}>
                        <span className="row-file-icon"><FileIcon /></span>
                        <span><strong>{document.originalFileName}</strong><small>{shortId(document.id)}</small></span>
                      </button>
                    </td>
                    <td data-label="Uploaded"><span className="date-cell">{formatDate(document.uploadedAt)}</span></td>
                    <td data-label="Status"><span className={`table-status status-${document.status.toLowerCase()}`}><span />{document.status}</span></td>
                    <td data-label="Quality">
                      {document.qualityScore == null ? "—" : (
                        <span className="table-score"><strong>{Math.round(document.qualityScore)}</strong><span className="score-bar"><i style={{ width: `${document.qualityScore}%` }} /></span></span>
                      )}
                    </td>
                    <td data-label="Checks">
                      {document.status === "Completed" ? (
                        <span className="check-summary">
                          <span className={document.documentDetected ? "pass" : "fail"}>{document.documentDetected ? "Boundary found" : "No boundary"}</span>
                          <span className={document.blurDetected === false ? "pass" : "fail"}>{document.blurDetected === false ? "Sharp" : "Blurred"}</span>
                        </span>
                      ) : <span className="unavailable-checks">Analysis unavailable</span>}
                    </td>
                    <td>
                      <button
                        className="icon-button delete-button"
                        type="button"
                        onClick={() => setPendingDeleteId(document.id)}
                        disabled={deletingId === document.id}
                        aria-label={`Delete ${document.originalFileName}`}
                      >
                        {deletingId === document.id ? <span className="spinner spinner-dark" /> : <TrashIcon />}
                      </button>
                    </td>
                  </tr>
                  {pendingDeleteId === document.id && (
                    <tr className="delete-confirmation-row">
                      <td colSpan={6}>
                        <div className="delete-confirmation" role="group" aria-label={`Confirm deletion of ${document.originalFileName}`}>
                          <span>Delete <strong>{document.originalFileName}</strong> and its saved images?</span>
                          <div>
                            <button type="button" className="button button-secondary" onClick={() => setPendingDeleteId(null)}>Cancel</button>
                            <button
                              type="button"
                              className="button button-danger"
                              onClick={() => {
                                setPendingDeleteId(null);
                                onDelete(document);
                              }}
                            >
                              Delete record
                            </button>
                          </div>
                        </div>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {documents.length > 0 && (
        <div className="history-pagination">
          <span>Showing {documents.length} of {total} documents</span>
          {documents.length < total && (
            <button className="button button-secondary" type="button" onClick={onLoadMore} disabled={isLoading || isLoadingMore}>
              {isLoadingMore ? "Loading…" : "Load more"}
            </button>
          )}
        </div>
      )}
    </section>
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

function shortId(id: string): string {
  return `ID ${id.slice(0, 8).toUpperCase()}`;
}
