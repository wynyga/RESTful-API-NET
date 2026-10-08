"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";

import { useProfile } from "@/components/AppFrame";
import ProjectForm from "@/components/ProjectForm";
import StatusBadge from "@/components/StatusBadge";
import { Badge, Button, Card, ConfirmDialog, EmptyState, ErrorState, PageHeader, Pagination, SearchInput, Select, SkeletonRows } from "@/components/ui";
import { api } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { useAction, useDebounced } from "@/lib/hooks";
import type { Project, ProjectQuery, ProjectStatus } from "@/lib/types";

const PAGE_SIZE = 8;

const SORTS: { value: string; label: string }[] = [
  { value: "name:asc", label: "Name, A to Z" },
  { value: "name:desc", label: "Name, Z to A" },
  { value: "endDate:asc", label: "Ends soonest" },
  { value: "endDate:desc", label: "Ends latest" },
  { value: "progress:desc", label: "Most progress" },
  { value: "progress:asc", label: "Least progress" },
];

const STATUS_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: "On Progress", label: "On Progress" },
  { value: "Completed", label: "Completed" },
  { value: "Overdue", label: "Overdue" },
];

export default function ProjectsPage() {
  // useSearchParams needs a Suspense boundary so the rest of the page can render first.
  return (
    <Suspense fallback={null}>
      <Projects />
    </Suspense>
  );
}

function Projects() {
  const profile = useProfile();
  const isAdmin = profile.role === "Admin";
  const params = useSearchParams();

  const [q, setQ] = useState("");
  const [status, setStatus] = useState<ProjectStatus | "">((params.get("status") as ProjectStatus | null) ?? "");
  const [sort, setSort] = useState("endDate:asc");
  const [page, setPage] = useState(1);
  const [form, setForm] = useState<{ project?: Project } | null>(null);
  const [deleting, setDeleting] = useState<Project | null>(null);
  const dq = useDebounced(q.trim());

  const [sortBy, sortOrder] = sort.split(":") as [NonNullable<ProjectQuery["sortBy"]>, "asc" | "desc"];
  const query: ProjectQuery = { search: dq, status, sortBy, sortOrder, page, pageSize: PAGE_SIZE };

  const projects = useQuery({
    queryKey: ["projects", query],
    queryFn: () => api.projects.list(query),
    placeholderData: keepPreviousData,
  });
  const remove = useAction(api.projects.remove, { success: "Project deleted", invalidate: ["projects", "summary"] });

  const filtered = Boolean(dq || status);

  return (
    <>
      <PageHeader
        title="Projects"
        subtitle={isAdmin ? "Create, edit and track every project." : "Read-only view. Ask an Admin to change a project."}
        actions={
          isAdmin ? (
            <Button variant="primary" onClick={() => setForm({})}>
              New project
            </Button>
          ) : (
            <Badge>Read-only</Badge>
          )
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <SearchInput
          value={q}
          onChange={(v) => {
            setQ(v);
            setPage(1);
          }}
          placeholder="Search name or description"
        />
        <Select
          label="Filter by status"
          value={status}
          onChange={(v) => {
            setStatus(v as ProjectStatus | "");
            setPage(1);
          }}
          options={STATUS_OPTIONS}
          className="w-auto"
        />
        <Select label="Sort by" value={sort} onChange={(v) => { setSort(v); setPage(1); }} options={SORTS} className="w-auto" />
      </div>

      <Card className="overflow-hidden">
        {projects.isPending ? (
          <SkeletonRows />
        ) : projects.isError ? (
          <ErrorState message={projects.error.message} onRetry={() => projects.refetch()} />
        ) : projects.data.items.length === 0 ? (
          <EmptyState
            title={filtered ? "No projects match" : "No projects yet"}
            hint={filtered ? "Try a different search or status." : isAdmin ? "Create the first project to get started." : "There is nothing to show yet."}
            action={!filtered && isAdmin ? <Button variant="primary" onClick={() => setForm({})}>New project</Button> : undefined}
          />
        ) : (
          <div className={`overflow-x-auto transition-opacity ${projects.isPlaceholderData ? "opacity-60" : ""}`}>
            <table className="w-full min-w-[40rem] text-left text-sm">
              <thead className="border-b border-line bg-surface2 text-xs text-muted">
                <tr>
                  <th scope="col" className="px-5 py-3 font-medium">Project</th>
                  <th scope="col" className="px-3 py-3 font-medium">Status</th>
                  <th scope="col" className="px-3 py-3 font-medium">Progress</th>
                  <th scope="col" className="px-3 py-3 font-medium">Timeline</th>
                  {isAdmin && (
                    <th scope="col" className="px-5 py-3 text-right font-medium">
                      <span className="sr-only">Actions</span>
                    </th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {projects.data.items.map((p) => (
                  <tr key={p.id} className="hover:bg-surface2">
                    <td className="max-w-xs px-5 py-3.5">
                      <p className="font-medium">{p.projectName}</p>
                      {p.description && <p className="truncate text-muted">{p.description}</p>}
                    </td>
                    <td className="px-3 py-3.5">
                      <StatusBadge status={p.status} />
                    </td>
                    <td className="px-3 py-3.5">
                      <div className="flex w-36 items-center gap-2">
                        <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-line" role="img" aria-label={`${p.progress}% complete`}>
                          <div
                            className="h-full rounded-full"
                            style={{
                              width: `${p.progress}%`,
                              background: p.status === "Completed" ? "var(--c-done)" : p.status === "Overdue" ? "var(--c-late)" : "var(--c-progress)",
                            }}
                          />
                        </div>
                        <span className="w-9 text-right tabular-nums">{p.progress}%</span>
                      </div>
                    </td>
                    <td className="px-3 py-3.5 whitespace-nowrap text-muted">
                      {formatDate(p.startDate)} → {formatDate(p.endDate)}
                    </td>
                    {isAdmin && (
                      <td className="px-5 py-3.5">
                        <div className="flex justify-end gap-2">
                          <Button size="sm" onClick={() => setForm({ project: p })}>Edit</Button>
                          <Button size="sm" variant="ghost" onClick={() => setDeleting(p)}>Delete</Button>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {projects.data && <Pagination meta={projects.data} onPage={setPage} />}
      </Card>

      <ProjectForm open={form !== null} onClose={() => setForm(null)} project={form?.project} />

      <ConfirmDialog
        open={deleting !== null}
        title={`Delete “${deleting?.projectName ?? "project"}”?`}
        message="This removes the project for everyone. It cannot be undone."
        confirmLabel="Delete project"
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          if (deleting) await remove.run(deleting.id);
          setDeleting(null);
        }}
      />
    </>
  );
}
