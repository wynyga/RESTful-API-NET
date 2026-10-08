// The browser-side API client. In normal mode every call goes to this app's own /api proxy,
// which adds the session token from an httpOnly cookie. In demo mode the same calls are
// answered in the browser from sample data, so the UI code never knows the difference.

import { demoRequest } from "./demo/handlers";
import type { AdminUser, Paged, Profile, Project, ProjectInput, ProjectQuery, Summary, Weather } from "./types";

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    /** Per-field validation messages (keys are camelCase, like the request fields). */
    public fields?: Record<string, string>,
  ) {
    super(message);
  }
}

let demo = false;

/** Called once at startup by the layout, before any request is made. */
export function configureApi(options: { demo: boolean }) {
  demo = options.demo;
}

export const isDemo = () => demo;

type Params = Record<string, string | number | undefined>;

function qs(params?: Params): string {
  if (!params) return "";
  const sp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== "") sp.set(k, String(v));
  }
  const s = sp.toString();
  return s ? `?${s}` : "";
}

type Raw = { status: number; json: unknown };

async function transport(method: string, path: string, body?: unknown): Promise<Raw> {
  if (demo) return demoRequest(method, path, body);
  try {
    const res = await fetch(`/api${path}`, {
      method,
      headers: body === undefined ? undefined : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const json = await res.json().catch(() => null);
    return { status: res.status, json };
  } catch {
    throw new ApiError("Cannot reach the server. Check your connection and try again.", 0);
  }
}

type Problem = { title?: string; detail?: string; errors?: Record<string, string[]>; message?: string };

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const { status, json } = await transport(method, path, body);

  if (status === 401 && typeof window !== "undefined" && !path.startsWith("/auth/")) {
    // The session is gone or expired: clear it and go to the sign-in page.
    void api.logout().finally(() => window.location.assign("/login"));
  }
  if (status >= 400) {
    const p = (json ?? {}) as Problem;
    const fields: Record<string, string> = {};
    for (const [key, messages] of Object.entries(p.errors ?? {})) {
      fields[key.charAt(0).toLowerCase() + key.slice(1)] = messages[0];
    }
    const message = p.detail || (Object.keys(fields).length ? Object.values(fields)[0] : p.title || p.message) || `Request failed (${status})`;
    throw new ApiError(message, status, Object.keys(fields).length ? fields : undefined);
  }
  return json as T;
}

export const api = {
  login: (email: string, password: string) => request<{ ok: true }>("POST", "/auth/login", { email, password }),
  register: (email: string, password: string) => request<unknown>("POST", "/auth/register", { email, password }),
  logout: () => request<{ ok: true }>("POST", "/auth/logout").catch(() => ({ ok: true as const })),
  me: () => request<Profile>("GET", "/profile"),

  summary: () => request<Summary>("GET", "/dashboard/summary"),

  projects: {
    list: (q: ProjectQuery = {}) => request<Paged<Project>>("GET", `/projects${qs(q as Params)}`),
    create: (p: ProjectInput) => request<Project>("POST", "/projects", p),
    update: (id: string, p: ProjectInput) => request<Project>("PUT", `/projects/${encodeURIComponent(id)}`, p),
    remove: (id: string) => request<{ message: string }>("DELETE", `/projects/${encodeURIComponent(id)}`),
  },

  admin: {
    users: () => request<AdminUser[]>("GET", "/admin/users"),
    setRole: (id: string, role: "Admin" | "User") => request<{ message: string }>("PUT", `/users/${encodeURIComponent(id)}/role`, { role }),
  },

  weather: (city: string) => request<Weather>("GET", `/weather/${encodeURIComponent(city)}`),
};
