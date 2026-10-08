"use client";

import { useState } from "react";

import { api } from "@/lib/api";
import { toInputDate } from "@/lib/format";
import { useAction } from "@/lib/hooks";
import type { Project, ProjectInput } from "@/lib/types";
import { Button, Modal, ModalActions, Select, TextArea, TextField } from "./ui";

type Props = {
  open: boolean;
  onClose: () => void;
  /** Present when editing; absent when creating. */
  project?: Project;
};

export default function ProjectForm({ open, onClose, project }: Props) {
  return (
    <Modal open={open} onClose={onClose} title={project ? "Edit project" : "New project"}>
      <Form project={project} onClose={onClose} />
    </Modal>
  );
}

const today = () => new Date().toISOString().slice(0, 10);

function Form({ project, onClose }: Omit<Props, "open">) {
  const [v, setV] = useState({
    projectName: project?.projectName ?? "",
    description: project?.description ?? "",
    // "Overdue" is derived by the API, so the editable status is only On Progress / Completed.
    status: (project?.status === "Completed" ? "Completed" : "On Progress") as ProjectInput["status"],
    startDate: project ? toInputDate(project.startDate) : today(),
    endDate: project ? toInputDate(project.endDate) : "",
    progress: String(project?.progress ?? 0),
  });
  const [error, setError] = useState<{ message: string; fields?: Record<string, string> } | null>(null);

  const save = useAction(
    (input: ProjectInput) => (project ? api.projects.update(project.id, input) : api.projects.create(input)),
    {
      success: (p) => (project ? `Saved “${p.projectName}”` : `Created “${p.projectName}”`),
      invalidate: ["projects", "summary"],
      toastError: false,
    },
  );

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const res = await save.run({
      projectName: v.projectName.trim(),
      description: v.description.trim(),
      status: v.status,
      startDate: v.startDate ? `${v.startDate}T00:00:00` : "",
      endDate: v.endDate ? `${v.endDate}T00:00:00` : "",
      progress: Number(v.progress),
    });
    if (res.ok) onClose();
    else setError({ message: res.error.message, fields: res.error.fields });
  }

  // Status and progress must agree, so each one nudges the other instead of leaving a trap.
  const setStatus = (status: ProjectInput["status"]) =>
    setV((c) => ({ ...c, status, progress: status === "Completed" ? "100" : c.progress === "100" ? "90" : c.progress }));
  const setProgress = (progress: string) =>
    setV((c) => ({ ...c, progress, status: Number(progress) === 100 ? "Completed" : c.status === "Completed" ? "On Progress" : c.status }));

  return (
    <form onSubmit={submit} className="space-y-4" noValidate>
      <TextField label="Project name" value={v.projectName} onChange={(e) => setV({ ...v, projectName: e.target.value })} error={error?.fields?.projectName} autoFocus required />
      <TextArea label="Description" value={v.description} onChange={(e) => setV({ ...v, description: e.target.value })} error={error?.fields?.description} />

      <div className="grid gap-4 sm:grid-cols-2">
        <TextField label="Start date" type="date" value={v.startDate} onChange={(e) => setV({ ...v, startDate: e.target.value })} error={error?.fields?.startDate} required />
        <TextField label="End date" type="date" value={v.endDate} onChange={(e) => setV({ ...v, endDate: e.target.value })} error={error?.fields?.endDate} required />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <p className="text-sm font-medium">Status</p>
          <Select
            label="Status"
            value={v.status}
            onChange={(s) => setStatus(s as ProjectInput["status"])}
            options={[
              { value: "On Progress", label: "On Progress" },
              { value: "Completed", label: "Completed" },
            ]}
          />
          {error?.fields?.status ? (
            <p className="text-xs text-danger">{error.fields.status}</p>
          ) : (
            <p className="text-xs text-muted">Overdue is worked out from the end date.</p>
          )}
        </div>
        <TextField
          label="Progress (%)"
          type="number"
          min={0}
          max={100}
          value={v.progress}
          onChange={(e) => setProgress(e.target.value)}
          error={error?.fields?.progress}
          hint="Completed means 100."
        />
      </div>

      {error && !error.fields && (
        <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
          {error.message}
        </p>
      )}

      <ModalActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button type="submit" variant="primary" loading={save.pending}>
          {project ? "Save changes" : "Create project"}
        </Button>
      </ModalActions>
    </form>
  );
}
