// Shapes returned by the ProjectTracker API. Keep in step with api/Models/DTOs.

export type Role = "Admin" | "User";

export type ProjectStatus = "On Progress" | "Completed" | "Overdue";

export type Profile = { id: string; email: string; role: Role };

export type Project = {
  id: string;
  projectName: string;
  description: string;
  status: ProjectStatus;
  startDate: string;
  endDate: string;
  progress: number;
};

export type ProjectInput = {
  projectName: string;
  description: string;
  status: Exclude<ProjectStatus, "Overdue">;
  startDate: string;
  endDate: string;
  progress: number;
};

export type Paged<T> = { items: T[]; page: number; pageSize: number; total: number; totalPages: number };

export type Summary = {
  totalProject: number;
  onProgress: number;
  completed: number;
  overdue: number;
  progressPercentage: number;
};

export type AdminUser = { id: string; email: string; role: Role };

export type Weather = { city: string; temperature: number; description: string; humidity: number };

export type ProjectQuery = {
  search?: string;
  status?: ProjectStatus | "";
  sortBy?: "name" | "status" | "startDate" | "endDate" | "progress";
  sortOrder?: "asc" | "desc";
  page?: number;
  pageSize?: number;
};
