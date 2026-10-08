"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { Button, TextField } from "@/components/ui";
import { ApiError, api } from "@/lib/api";
import { DEMO_ACCOUNTS } from "@/lib/demo/handlers";

export default function LoginForm({ demo }: { demo: boolean }) {
  const router = useRouter();
  const qc = useQueryClient();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<ApiError | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      if (mode === "register") {
        await api.register(email, password);
        setNotice("Account created. Signing you in…");
      }
      await api.login(email, password);
      qc.clear();
      router.replace("/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError("Something went wrong", 0));
      setNotice(null);
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="mt-8 space-y-4" noValidate>
      {demo && (
        <div className="rounded-xl border border-line bg-surface p-4 text-sm">
          <p className="font-medium">Demo mode</p>
          <p className="mt-1 text-muted">Sample data, no server. Pick an account:</p>
          <div className="mt-3 flex flex-wrap gap-2">
            {DEMO_ACCOUNTS.map((a) => (
              <Button
                key={a.email}
                size="sm"
                onClick={() => {
                  setMode("login");
                  setEmail(a.email);
                  setPassword(a.password);
                }}
              >
                {a.role}: {a.email}
              </Button>
            ))}
          </div>
          <p className="mt-2 text-xs text-muted">Admins can edit projects and manage users; Users can only read.</p>
        </div>
      )}

      <div role="group" aria-label="Sign in or create an account" className="inline-flex rounded-lg border border-line bg-surface p-0.5 text-sm">
        {(["login", "register"] as const).map((m) => (
          <button
            key={m}
            type="button"
            aria-pressed={mode === m}
            onClick={() => {
              setMode(m);
              setError(null);
            }}
            className={`rounded-md px-3 py-1.5 transition-colors ${mode === m ? "bg-accent-soft font-medium text-ink" : "text-muted hover:text-ink"}`}
          >
            {m === "login" ? "Sign in" : "Create account"}
          </button>
        ))}
      </div>

      <TextField
        label="Email"
        type="email"
        autoComplete="username"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        error={error?.fields?.email}
        required
      />
      <TextField
        label="Password"
        type="password"
        autoComplete={mode === "login" ? "current-password" : "new-password"}
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        error={error?.fields?.password}
        hint={mode === "register" ? "At least 6 characters." : undefined}
        required
      />

      {error && !error.fields && (
        <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
          {error.message}
        </p>
      )}
      {notice && (
        <p role="status" className="rounded-lg bg-accent-soft px-3 py-2 text-sm">
          {notice}
        </p>
      )}

      <Button type="submit" variant="primary" loading={busy} className="w-full">
        {mode === "login" ? "Sign in" : "Create account"}
      </Button>
    </form>
  );
}
