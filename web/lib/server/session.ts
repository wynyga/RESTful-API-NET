import "server-only";

export const COOKIE = "pt_session";

/** Base URL of the ProjectTracker API. Unset means demo mode (sample data in the browser). */
export const API_URL = process.env.PROJECTTRACKER_API_URL?.trim().replace(/\/+$/, "") || undefined;

export const isDemo = !API_URL;
