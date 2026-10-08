"use client";

import { useState } from "react";

import { percent } from "@/lib/format";
import type { ProjectStatus, Summary } from "@/lib/types";
import { StatusIcon } from "./StatusBadge";
import { Button } from "./ui";

const SEGMENTS: { status: ProjectStatus; color: string; pick: (s: Summary) => number }[] = [
  { status: "Completed", color: "var(--c-done)", pick: (s) => s.completed },
  { status: "On Progress", color: "var(--c-progress)", pick: (s) => s.onProgress },
  { status: "Overdue", color: "var(--c-late)", pick: (s) => s.overdue },
];

/**
 * Part-to-whole as one stacked bar. Green and red are not safe to tell apart by colour alone,
 * so every segment is also a focusable, labelled mark with an icon in the legend, and the same
 * numbers are available as a table.
 */
export default function StatusChart({ summary }: { summary: Summary }) {
  const [asTable, setAsTable] = useState(false);
  const [active, setActive] = useState<ProjectStatus | null>(null);
  const total = summary.totalProject;
  const rows = SEGMENTS.map((s) => ({ ...s, count: s.pick(summary) }));
  const hovered = rows.find((r) => r.status === active);

  return (
    <div>
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-base font-semibold">Projects by status</h2>
        <Button size="sm" variant="ghost" onClick={() => setAsTable((v) => !v)} aria-pressed={asTable}>
          {asTable ? "View as chart" : "View as table"}
        </Button>
      </div>

      {total === 0 ? (
        <p className="mt-6 text-sm text-muted">No projects yet.</p>
      ) : asTable ? (
        <table className="mt-4 w-full text-left text-sm">
          <caption className="sr-only">Number of projects per status</caption>
          <thead className="text-xs text-muted">
            <tr>
              <th scope="col" className="py-2 font-medium">Status</th>
              <th scope="col" className="py-2 text-right font-medium">Projects</th>
              <th scope="col" className="py-2 text-right font-medium">Share</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {rows.map((r) => (
              <tr key={r.status}>
                <th scope="row" className="py-2 font-medium">
                  <span className="inline-flex items-center gap-2">
                    <StatusIcon status={r.status} /> {r.status}
                  </span>
                </th>
                <td className="py-2 text-right tabular-nums">{r.count}</td>
                <td className="py-2 text-right tabular-nums">{percent(r.count, total)}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <>
          {/* min-h keeps the tooltip row from shifting the layout when it appears */}
          <p className="mt-4 min-h-5 text-sm text-muted" aria-live="polite">
            {hovered ? `${hovered.status}: ${hovered.count} of ${total} projects (${percent(hovered.count, total)}%)` : `${total} projects in total`}
          </p>
          <div role="group" aria-label="Projects by status" className="mt-2 flex h-9 gap-0.5">
            {rows
              .filter((r) => r.count > 0)
              .map((r) => (
                <button
                  key={r.status}
                  type="button"
                  aria-label={`${r.status}: ${r.count} of ${total} projects`}
                  onMouseEnter={() => setActive(r.status)}
                  onMouseLeave={() => setActive(null)}
                  onFocus={() => setActive(r.status)}
                  onBlur={() => setActive(null)}
                  className="flex min-w-6 items-center justify-center rounded-[4px] text-xs font-semibold text-white outline-offset-2 transition-opacity first:rounded-l-md last:rounded-r-md"
                  style={{ flexGrow: r.count, flexBasis: 0, background: r.color, opacity: active && active !== r.status ? 0.55 : 1 }}
                >
                  {r.count}
                </button>
              ))}
          </div>
          <ul className="mt-4 flex flex-wrap gap-x-6 gap-y-2 text-sm">
            {rows.map((r) => (
              <li key={r.status} className="flex items-center gap-2">
                <StatusIcon status={r.status} />
                <span>{r.status}</span>
                <span className="text-muted tabular-nums">
                  {r.count} · {percent(r.count, total)}%
                </span>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
