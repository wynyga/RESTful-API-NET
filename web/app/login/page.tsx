import type { Metadata } from "next";

import { isDemo } from "@/lib/server/session";
import LoginForm from "./LoginForm";

export const metadata: Metadata = { title: "Sign in" };

export default function LoginPage() {
  return (
    <div className="grid min-h-dvh place-items-center px-4 py-10">
      <div className="w-full max-w-sm">
        <p className="text-3xl font-semibold tracking-tight">
          ProjectTracker<span className="text-accent">.</span>
        </p>
        <p className="mt-2 text-sm text-muted">Sign in to see where every project stands.</p>
        <LoginForm demo={isDemo} />
      </div>
    </div>
  );
}
