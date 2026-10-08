"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";

import StatusChart from "@/components/StatusChart";
import WeatherCard from "@/components/WeatherCard";
import { Card, EmptyState, ErrorState, PageHeader, SkeletonRows } from "@/components/ui";
import { api } from "@/lib/api";
import { dueLabel, formatDate } from "@/lib/format";

function Kpi({ label, value, hint, href, tone }: { label: string; value: string | number; hint?: string; href?: string; tone?: "late" }) {
  const body = (
    <>
      <p className="text-sm text-muted">{label}</p>
      <p className="mt-2 text-4xl font-semibold tabular-nums">{value}</p>
      {hint && (
        <p className="mt-2 flex items-center gap-1.5 text-xs text-muted">
          {tone === "late" && <span className="size-2 rounded-full bg-c-late" aria-hidden="true" />}
          {hint}
        </p>
      )}
    </>
  );
  const cls = "rounded-2xl border border-line bg-surface p-5";
  return href ? (
    <Link href={href} className={`${cls} transition-colors hover:border-accent`}>
      {body}
    </Link>
  ) : (
    <div className={cls}>{body}</div>
  );
}

export default function Dashboard() {
  const summary = useQuery({ queryKey: ["summary"], queryFn: api.summary });
  const late = useQuery({
    queryKey: ["projects", { status: "Overdue", sortBy: "endDate", pageSize: 5 }],
    queryFn: () => api.projects.list({ status: "Overdue", sortBy: "endDate", sortOrder: "asc", pageSize: 5 }),
  });

  return (
    <>
      <PageHeader title="Dashboard" subtitle="Where every project stands today." />

      {summary.isError ? (
        <Card>
          <ErrorState message={summary.error.message} onRetry={() => summary.refetch()} />
        </Card>
      ) : (
        <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-5">
          {summary.isPending ? (
            Array.from({ length: 5 }, (_, i) => <div key={i} className="h-[7.5rem] animate-pulse rounded-2xl bg-line" aria-hidden="true" />)
          ) : (
            <>
              <Kpi label="Projects" value={summary.data.totalProject} href="/projects" />
              <Kpi label="On progress" value={summary.data.onProgress} href="/projects?status=On%20Progress" />
              <Kpi label="Completed" value={summary.data.completed} href="/projects?status=Completed" />
              <Kpi
                label="Overdue"
                value={summary.data.overdue}
                href="/projects?status=Overdue"
                hint={summary.data.overdue === 0 ? "Nothing late" : "Past their end date"}
                tone={summary.data.overdue > 0 ? "late" : undefined}
              />
              <Kpi label="Average progress" value={`${summary.data.progressPercentage}%`} hint="across all projects" />
            </>
          )}
        </div>
      )}

      <div className="mt-6 grid gap-4 lg:grid-cols-5">
        <Card className="p-5 lg:col-span-3">{summary.data ? <StatusChart summary={summary.data} /> : <div className="h-40 animate-pulse rounded-xl bg-line" aria-hidden="true" />}</Card>
        <div className="lg:col-span-2">
          <WeatherCard />
        </div>
      </div>

      <Card className="mt-4 overflow-hidden">
        <div className="flex items-center justify-between gap-3 px-5 pt-5">
          <h2 className="text-base font-semibold">Needs attention</h2>
          <Link href="/projects?status=Overdue" className="text-sm text-accent hover:underline">
            All overdue projects
          </Link>
        </div>
        <div className="mt-3">
          {late.isPending ? (
            <SkeletonRows rows={3} />
          ) : late.isError ? (
            <ErrorState message={late.error.message} onRetry={() => late.refetch()} />
          ) : late.data.items.length === 0 ? (
            <EmptyState title="Nothing is overdue" hint="Projects past their end date show up here, most overdue first." />
          ) : (
            <ul className="divide-y divide-line border-t border-line">
              {late.data.items.map((p) => (
                <li key={p.id} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-3.5">
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{p.projectName}</p>
                    <p className="text-sm text-muted">Ended {formatDate(p.endDate)}</p>
                  </div>
                  <div className="flex w-40 items-center gap-2 text-sm">
                    <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-line" role="img" aria-label={`${p.progress}% complete`}>
                      <div className="h-full rounded-full bg-c-late" style={{ width: `${p.progress}%` }} />
                    </div>
                    <span className="w-9 text-right tabular-nums">{p.progress}%</span>
                  </div>
                  <span className="w-28 text-right text-sm text-muted">{dueLabel(p.endDate)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </Card>
    </>
  );
}
