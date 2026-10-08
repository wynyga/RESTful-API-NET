const dateFmt = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric" });

/** The API sends dates without a zone ("2026-10-08T00:00:00"); read them as UTC calendar days. */
const parse = (iso: string) => Date.parse(/Z|[+-]\d\d:\d\d$/.test(iso) ? iso : `${iso}Z`);

export function formatDate(iso: string): string {
  const t = parse(iso);
  return Number.isNaN(t) ? "—" : dateFmt.format(new Date(t));
}

/** YYYY-MM-DD for an <input type="date">. */
export const toInputDate = (iso: string) => iso.slice(0, 10);

/** Whole days between the end date and today (UTC); positive means it is late. */
export function daysLate(endIso: string, now = new Date()): number {
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  return Math.round((today - parse(endIso)) / 86_400_000);
}

export function dueLabel(endIso: string, now = new Date()): string {
  const d = daysLate(endIso, now);
  if (d > 0) return `${d} day${d === 1 ? "" : "s"} overdue`;
  if (d === 0) return "Due today";
  return `Due in ${-d} day${d === -1 ? "" : "s"}`;
}

export const percent = (part: number, whole: number) => (whole === 0 ? 0 : Math.round((part / whole) * 100));
