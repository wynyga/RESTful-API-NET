// An in-browser stand-in for the ProjectTracker API, used in demo mode. It follows the real
// API's rules: Overdue is derived from the end date, status and progress must agree, only
// Admins may write, ids are opaque, errors are RFC 9457 problems. Data lives in localStorage,
// so every visitor works on a private copy that survives reloads.

import type { Role } from "../types";

type Raw = { status: number; json: unknown };
type Stored = "On Progress" | "Completed";
type DUser = { id: string; email: string; password: string; role: Role };
type DProject = { id: string; projectName: string; description: string; status: Stored; startDate: string; endDate: string; progress: number };
type DB = { version: number; users: DUser[]; projects: DProject[] };

const DB_KEY = "projecttracker-demo-db";
const SESSION_KEY = "projecttracker-demo-session";
const VERSION = 1;
const DAY = 86_400_000;
const STATUSES = ["On Progress", "Completed", "Overdue"];
const SORTS = ["name", "status", "startDate", "endDate", "progress"];

export const DEMO_ACCOUNTS = [
  { email: "admin@demo.test", password: "demo1234", role: "Admin" as const },
  { email: "user@demo.test", password: "demo1234", role: "User" as const },
];

// ---- storage ------------------------------------------------------------

let memory: DB | null = null;

function randomId(): string {
  const alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
  let out = "";
  const bytes = crypto.getRandomValues(new Uint8Array(10));
  for (const b of bytes) out += alphabet[b % alphabet.length];
  return out;
}

const todayUtc = () => {
  const d = new Date();
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate());
};
const iso = (ms: number) => new Date(ms).toISOString().slice(0, 10) + "T00:00:00";
const parse = (s: string) => Date.parse(/Z|[+-]\d\d:\d\d$/.test(s) ? s : s + "Z");

function seed(): DB {
  const t = todayUtc();
  // [name, description, start offset, end offset, progress, completed]
  const rows: [string, string, number, number, number, boolean?][] = [
    ["Renovasi Gedung Kantor Pusat", "Perbaikan atap dan instalasi listrik gedung utama.", -75, -10, 100, true],
    ["Pembangunan Gudang Logistik", "Gudang 1.200 m2 dengan sistem rak bertingkat.", -90, 20, 72],
    ["Migrasi Server ke Cloud", "Pemindahan aplikasi internal ke infrastruktur cloud.", -60, 35, 55],
    ["Instalasi Panel Surya", "Panel surya 50 kWp untuk kantor cabang.", -120, -15, 80],
    ["Pembangunan Jalan Akses Proyek", "Jalan akses sepanjang 3 km menuju lokasi proyek.", -45, 60, 30],
    ["Digitalisasi Arsip Dokumen", "Pemindaian dan pengindeksan arsip fisik.", -150, -30, 100, true],
    ["Pengadaan Peralatan Laboratorium", "Pengadaan dan kalibrasi peralatan uji.", -30, 14, 45],
    ["Audit Keselamatan Kerja Tahunan", "Audit K3 di seluruh lokasi operasional.", -20, 40, 15],
    ["Pembangunan Pagar dan Pos Jaga", "Pagar keliling dan dua pos jaga baru.", -100, -5, 90],
    ["Upgrade Jaringan Kantor", "Penggantian switch dan penataan ulang kabel jaringan.", -50, 25, 65],
    ["Pelatihan Operator Alat Berat", "Program sertifikasi untuk 40 operator.", -80, -40, 100, true],
    ["Pembangunan Mess Karyawan", "Mess dua lantai untuk 60 karyawan.", -10, 150, 8],
  ];
  return {
    version: VERSION,
    users: DEMO_ACCOUNTS.map((a) => ({ id: randomId(), email: a.email, password: a.password, role: a.role })),
    projects: rows.map(([projectName, description, s, e, progress, done]) => ({
      id: randomId(), projectName, description, status: done ? "Completed" : "On Progress",
      startDate: iso(t + s * DAY), endDate: iso(t + e * DAY), progress,
    })),
  };
}

function db(): DB {
  if (memory) return memory;
  try {
    const raw = window.localStorage.getItem(DB_KEY);
    const parsed = raw ? (JSON.parse(raw) as DB) : null;
    if (parsed && parsed.version === VERSION) return (memory = parsed);
  } catch {
    // blocked or corrupt storage: start from the seed
  }
  memory = seed();
  persist();
  return memory;
}

function persist() {
  try {
    window.localStorage.setItem(DB_KEY, JSON.stringify(memory));
  } catch {
    // storage full or blocked: the demo keeps working in memory
  }
}

/** Throws the sample data away and starts over. */
export function resetDemo() {
  memory = null;
  try {
    window.localStorage.removeItem(DB_KEY);
    window.localStorage.removeItem(SESSION_KEY);
  } catch {
    // ignore
  }
}

// ---- helpers ------------------------------------------------------------

const ok = (json: unknown, status = 200): Raw => ({ status, json });
const problem = (status: number, title: string, detail?: string, errors?: Record<string, string[]>): Raw => ({
  status, json: { title, status, ...(detail ? { detail } : {}), ...(errors ? { errors } : {}) },
});
const invalid = (errors: Record<string, string[]>) =>
  problem(400, "One or more validation errors occurred.", undefined, errors);

function sessionUser(): DUser | null {
  try {
    const id = window.localStorage.getItem(SESSION_KEY);
    return id ? (db().users.find((u) => u.id === id) ?? null) : null;
  } catch {
    return null;
  }
}

const effective = (p: DProject, today: number) =>
  p.status === "Completed" ? "Completed" : parse(p.endDate) < today ? "Overdue" : "On Progress";

const view = (p: DProject, today: number) => ({
  id: p.id, projectName: p.projectName, description: p.description, status: effective(p, today),
  startDate: p.startDate, endDate: p.endDate, progress: p.progress,
});

// ---- entry point --------------------------------------------------------

export async function demoRequest(method: string, path: string, body?: unknown): Promise<Raw> {
  // A touch of latency so loading states behave as they would against a real server.
  await new Promise((r) => setTimeout(r, 50 + Math.random() * 110));

  const url = new URL(path, "http://demo.local");
  const seg = url.pathname.split("/").filter(Boolean);
  const q = url.searchParams;
  const b = (body ?? {}) as Record<string, unknown>;
  const user = sessionUser();
  const needAuth = (): Raw | null => (user ? null : problem(401, "Unauthorized"));
  const needAdmin = (): Raw | null => needAuth() ?? (user!.role === "Admin" ? null : problem(403, "Forbidden"));

  // ---- auth
  if (seg[0] === "auth") {
    if (seg[1] === "login" && method === "POST") return login(b);
    if (seg[1] === "register" && method === "POST") return register(b);
    if (seg[1] === "logout" && method === "POST") {
      try {
        window.localStorage.removeItem(SESSION_KEY);
      } catch {
        // ignore
      }
      return ok({ ok: true });
    }
  }
  if (seg[0] === "profile" && method === "GET") {
    return needAuth() ?? ok({ id: user!.id, email: user!.email, role: user!.role });
  }

  if (seg[0] === "dashboard" && seg[1] === "summary" && method === "GET") return needAuth() ?? summary();

  if (seg[0] === "projects") {
    const id = seg[1];
    if (!id && method === "GET") return needAuth() ?? listProjects(q);
    if (!id && method === "POST") return needAdmin() ?? createProject(b);
    if (id && method === "GET") return needAuth() ?? getProject(id);
    if (id && method === "PUT") return needAdmin() ?? updateProject(id, b);
    if (id && method === "DELETE") return needAdmin() ?? deleteProject(id);
  }

  if (seg[0] === "admin" && seg[1] === "users" && method === "GET") {
    return needAdmin() ?? ok(db().users.map((u) => ({ id: u.id, email: u.email, role: u.role })));
  }
  if (seg[0] === "users" && seg[2] === "role" && method === "PUT") return needAdmin() ?? setRole(seg[1], b);

  if (seg[0] === "weather" && seg[1] && method === "GET") return needAuth() ?? weather(decodeURIComponent(seg[1]));

  return problem(404, "Not Found");
}

// ---- auth ---------------------------------------------------------------

function login(b: Record<string, unknown>): Raw {
  const email = String(b.email ?? "").trim().toLowerCase();
  const password = String(b.password ?? "");
  const errors: Record<string, string[]> = {};
  if (!email) errors.Email = ["The Email field is required."];
  if (!password) errors.Password = ["The Password field is required."];
  if (Object.keys(errors).length) return invalid(errors);

  const user = db().users.find((u) => u.email === email && u.password === password);
  if (!user) return problem(401, "Unauthorized", "Email atau password salah.");
  try {
    window.localStorage.setItem(SESSION_KEY, user.id);
  } catch {
    return problem(500, "Storage unavailable", "Browser storage is blocked, so the demo cannot keep you signed in.");
  }
  return ok({ ok: true });
}

function register(b: Record<string, unknown>): Raw {
  const email = String(b.email ?? "").trim().toLowerCase();
  const password = String(b.password ?? "");
  const errors: Record<string, string[]> = {};
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.Email = ["The Email field is not a valid e-mail address."];
  if (password.length < 6) errors.Password = ["Password minimal 6 karakter."];
  if (Object.keys(errors).length) return invalid(errors);

  const data = db();
  if (data.users.some((u) => u.email === email)) return problem(409, "Conflict", "Email sudah digunakan.");
  const user: DUser = { id: randomId(), email, password, role: "User" };
  data.users.push(user);
  persist();
  return ok({ message: "Registrasi berhasil.", data: { id: user.id, email, role: "User" } }, 201);
}

// ---- projects -----------------------------------------------------------

function summary(): Raw {
  const t = todayUtc();
  const all = db().projects;
  const statuses = all.map((p) => effective(p, t));
  const avg = all.length ? all.reduce((s, p) => s + p.progress, 0) / all.length : 0;
  return ok({
    totalProject: all.length,
    onProgress: statuses.filter((s) => s === "On Progress").length,
    completed: statuses.filter((s) => s === "Completed").length,
    overdue: statuses.filter((s) => s === "Overdue").length,
    progressPercentage: Math.round(avg * 100) / 100,
  });
}

function listProjects(q: URLSearchParams): Raw {
  const errors: Record<string, string[]> = {};
  const status = q.get("status");
  const sortBy = q.get("sortBy") ?? "name";
  const sortOrder = q.get("sortOrder") ?? "asc";
  const page = Number(q.get("page") ?? 1);
  const pageSize = Number(q.get("pageSize") ?? 10);
  if (status && !STATUSES.includes(status)) errors.Status = ["Status harus berupa 'On Progress', 'Completed', atau 'Overdue'."];
  if (!SORTS.includes(sortBy)) errors.SortBy = ["SortBy harus salah satu dari: name, status, startDate, endDate, progress."];
  if (!["asc", "desc"].includes(sortOrder)) errors.SortOrder = ["SortOrder harus 'asc' atau 'desc'."];
  if (!Number.isInteger(page) || page < 1) errors.Page = ["The field Page must be between 1 and 2147483647."];
  if (!Number.isInteger(pageSize) || pageSize < 1 || pageSize > 100) errors.PageSize = ["The field PageSize must be between 1 and 100."];
  if (Object.keys(errors).length) return invalid(errors);

  const t = todayUtc();
  const term = (q.get("search") ?? "").trim().toLowerCase();
  let rows = db().projects.filter(
    (p) => !term || p.projectName.toLowerCase().includes(term) || p.description.toLowerCase().includes(term),
  );
  if (status) rows = rows.filter((p) => effective(p, t) === status);

  const key = (p: DProject): string | number =>
    ({
      name: p.projectName.toLowerCase(), status: effective(p, t), startDate: parse(p.startDate),
      endDate: parse(p.endDate), progress: p.progress,
    })[sortBy as "name"];
  const dir = sortOrder === "desc" ? -1 : 1;
  rows = [...rows].sort((a, c) => {
    const x = key(a);
    const y = key(c);
    return (x < y ? -1 : x > y ? 1 : 0) * dir || a.id.localeCompare(c.id);
  });

  const total = rows.length;
  return ok({
    items: rows.slice((page - 1) * pageSize, page * pageSize).map((p) => view(p, t)),
    page, pageSize, total, totalPages: Math.ceil(total / pageSize),
  });
}

function getProject(id: string): Raw {
  const p = db().projects.find((x) => x.id === id);
  return p ? ok(view(p, todayUtc())) : problem(404, "Not Found", "Project tidak ditemukan atau ID tidak valid.");
}

function checkProject(b: Record<string, unknown>): { errors: Record<string, string[]>; value?: Omit<DProject, "id"> } {
  const errors: Record<string, string[]> = {};
  const name = String(b.projectName ?? "").trim();
  const status = String(b.status ?? "");
  const progress = Number(b.progress);
  const start = typeof b.startDate === "string" ? parse(b.startDate) : NaN;
  const end = typeof b.endDate === "string" ? parse(b.endDate) : NaN;

  if (!name) errors.ProjectName = ["The ProjectName field is required."];
  else if (name.length > 255) errors.ProjectName = ["The field ProjectName must be a string with a maximum length of 255."];
  if (!STATUSES.includes(status)) errors.Status = ["Status harus berupa 'On Progress', 'Completed', atau 'Overdue'."];
  if (Number.isNaN(start)) errors.StartDate = ["The StartDate field is required."];
  if (Number.isNaN(end)) errors.EndDate = ["The EndDate field is required."];
  if (!Number.isInteger(progress) || progress < 0 || progress > 100) errors.Progress = ["Progress harus berada dalam rentang 0 hingga 100."];

  if (!Number.isNaN(start) && !Number.isNaN(end) && end < start) errors.EndDate = ["End Date tidak boleh lebih awal dari Start Date."];
  if (!errors.Progress && !errors.Status) {
    if (status === "Completed" && progress !== 100) errors.Progress = ["Project berstatus 'Completed' harus memiliki Progress 100."];
    else if (status !== "Completed" && progress === 100) errors.Status = ["Progress 100 berarti project harus berstatus 'Completed'."];
  }
  if (Object.keys(errors).length) return { errors };

  return {
    errors,
    value: {
      projectName: name, description: String(b.description ?? ""), status: status === "Completed" ? "Completed" : "On Progress",
      startDate: iso(start), endDate: iso(end), progress,
    },
  };
}

function createProject(b: Record<string, unknown>): Raw {
  const { errors, value } = checkProject(b);
  if (!value) return invalid(errors);
  const data = db();
  if (data.projects.some((p) => p.projectName === value.projectName)) return problem(409, "Conflict", "Nama project sudah digunakan.");
  const p: DProject = { id: randomId(), ...value };
  data.projects.push(p);
  persist();
  return ok(view(p, todayUtc()), 201);
}

function updateProject(id: string, b: Record<string, unknown>): Raw {
  const data = db();
  const p = data.projects.find((x) => x.id === id);
  if (!p) return problem(404, "Not Found", "Project tidak ditemukan atau ID tidak valid.");
  const { errors, value } = checkProject(b);
  if (!value) return invalid(errors);
  if (value.projectName !== p.projectName && data.projects.some((x) => x.projectName === value.projectName)) {
    return problem(409, "Conflict", "Nama project sudah digunakan.");
  }
  Object.assign(p, value);
  persist();
  return ok(view(p, todayUtc()));
}

function deleteProject(id: string): Raw {
  const data = db();
  if (!data.projects.some((x) => x.id === id)) return problem(404, "Not Found", "Project tidak ditemukan atau ID tidak valid.");
  data.projects = data.projects.filter((x) => x.id !== id);
  persist();
  return ok({ message: "Project berhasil dihapus." });
}

// ---- admin --------------------------------------------------------------

function setRole(id: string, b: Record<string, unknown>): Raw {
  const role = String(b.role ?? "");
  if (!["Admin", "User"].includes(role)) return invalid({ Role: ["Role harus berupa 'Admin' atau 'User'."] });
  const target = db().users.find((u) => u.id === id);
  if (!target) return problem(400, "Bad Request", "ID User tidak valid.");
  target.role = role as Role;
  persist();
  return ok({ message: `Role user ${target.email} berhasil diupdate menjadi ${role}.` });
}

// ---- weather ------------------------------------------------------------

const CONDITIONS = ["Sunny", "Partly cloudy", "Light rain shower", "Cloudy", "Patchy rain nearby", "Clear"];

/** A deterministic fake for the third-party weather service, including its failure modes. */
function weather(cityRaw: string): Raw {
  const city = cityRaw.trim();
  if (!/^[\p{L}\p{N} .'\-]{1,60}$/u.test(city)) return problem(400, "Bad Request", "Nama kota tidak valid.");

  switch (city.toLowerCase()) {
    case "atlantis":
      return problem(404, "Not Found", `Kota '${city}' tidak ditemukan.`);
    case "slowville":
      return problem(504, "Gateway Timeout", "Layanan cuaca sedang tidak merespons (Timeout). Silakan coba lagi nanti.");
    case "glitch":
      return problem(502, "Bad Gateway", "Menerima data yang tidak valid dari layanan cuaca.");
    case "offline":
      return problem(502, "Bad Gateway", "Gagal terhubung ke layanan cuaca saat ini.");
  }
  let h = 0;
  for (const ch of city.toLowerCase()) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return ok({ city, temperature: 22 + (h % 11), description: CONDITIONS[h % CONDITIONS.length], humidity: 60 + (h % 31) });
}
