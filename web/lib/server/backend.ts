import "server-only";

import { API_URL } from "./session";

export type BackendResult = { status: number; json: unknown };

const problem = (status: number, title: string, detail: string) => ({ title, status, detail });

/** Calls the real API and returns its status and JSON body (always JSON, even when the API is down). */
export async function backend(
  method: string,
  path: string,
  opts: { search?: string; body?: string; token?: string } = {},
): Promise<BackendResult> {
  if (!API_URL) return { status: 503, json: problem(503, "Service Unavailable", "PROJECTTRACKER_API_URL is not configured.") };

  const headers: Record<string, string> = {};
  if (opts.token) headers.Authorization = `Bearer ${opts.token}`;
  if (opts.body) headers["Content-Type"] = "application/json";

  try {
    const res = await fetch(`${API_URL}${path}${opts.search ?? ""}`, {
      method,
      headers,
      body: opts.body,
      cache: "no-store",
      signal: AbortSignal.timeout(15_000),
    });
    const text = await res.text();
    if (!text) return { status: res.status, json: res.ok ? {} : problem(res.status, res.statusText || "Error", "The API returned an empty response.") };
    try {
      return { status: res.status, json: JSON.parse(text) };
    } catch {
      return { status: 502, json: problem(502, "Bad Gateway", "The API returned an unexpected response.") };
    }
  } catch {
    return { status: 502, json: problem(502, "Bad Gateway", "Cannot reach the ProjectTracker API.") };
  }
}
