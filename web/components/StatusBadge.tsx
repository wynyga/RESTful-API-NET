import type { ProjectStatus } from "@/lib/types";

/**
 * Status is shown as icon + label (never colour alone), because green and red cannot be told
 * apart by everyone. The icon carries the colour; the text stays in the normal ink colour.
 */
export function StatusIcon({ status, className = "size-4" }: { status: ProjectStatus; className?: string }) {
  const common = { className, viewBox: "0 0 20 20", fill: "none", stroke: "currentColor", strokeWidth: 2, strokeLinecap: "round" as const, strokeLinejoin: "round" as const, "aria-hidden": true };
  if (status === "Completed") {
    return (
      <svg {...common} style={{ color: "var(--c-done)" }}>
        <circle cx="10" cy="10" r="8" />
        <path d="m6.5 10.2 2.4 2.4 4.6-5" />
      </svg>
    );
  }
  if (status === "Overdue") {
    return (
      <svg {...common} style={{ color: "var(--c-late)" }}>
        <path d="M10 2.5 18 16.5H2L10 2.5Z" />
        <path d="M10 8v3.5M10 14v.1" />
      </svg>
    );
  }
  return (
    <svg {...common} style={{ color: "var(--c-progress)" }}>
      <circle cx="10" cy="10" r="8" />
      <path d="M10 5.5V10l3 2" />
    </svg>
  );
}

export default function StatusBadge({ status }: { status: ProjectStatus }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-line bg-surface2 px-2.5 py-0.5 text-xs font-medium whitespace-nowrap text-ink">
      <StatusIcon status={status} className="size-3.5" />
      {status}
    </span>
  );
}
