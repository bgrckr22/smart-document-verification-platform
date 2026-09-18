import type { DocumentRecord, PagedResponse, ProblemDetails } from "./types";

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim() ?? "";
const apiBaseUrl = configuredBaseUrl.replace(/\/$/, "");

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly traceId?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, init);
  if (!response.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      problem = undefined;
    }
    throw new ApiError(
      problem?.detail ?? problem?.title ?? `Request failed with status ${response.status}.`,
      response.status,
      problem?.traceId,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export async function uploadDocument(file: File): Promise<DocumentRecord> {
  const form = new FormData();
  form.append("file", file);
  return request<DocumentRecord>("/api/documents", {
    method: "POST",
    body: form,
  });
}

export function listDocuments(skip = 0): Promise<PagedResponse<DocumentRecord>> {
  return request<PagedResponse<DocumentRecord>>(`/api/documents?skip=${skip}&take=50`);
}

export function checkReadiness(): Promise<{ status: string }> {
  return request<{ status: string }>("/health/ready");
}

export function deleteDocument(id: string): Promise<void> {
  return request<void>(`/api/documents/${id}`, { method: "DELETE" });
}

export function resolveImageUrl(path: string): string {
  return `${apiBaseUrl}${path}`;
}

export const apiDocsUrl = `${apiBaseUrl}/swagger/index.html`;
