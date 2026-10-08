"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { createContext, useContext } from "react";

import { api } from "@/lib/api";
import type { Profile } from "@/lib/types";
import ThemeToggle from "./ThemeToggle";
import { Badge, Button, Spinner } from "./ui";

const ProfileContext = createContext<Profile | null>(null);

/** The signed-in user. Only usable below <AppFrame>, which does not render children until it is known. */
export const useProfile = () => {
  const profile = useContext(ProfileContext);
  if (!profile) throw new Error("useProfile must be used inside AppFrame");
  return profile;
};

/**
 * Gate and chrome for every signed-in page. The profile request doubles as the "am I logged in?"
 * check: a 401 makes the API client clear the session and send the visitor to /login.
 */
export default function AppFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const qc = useQueryClient();
  const me = useQuery({ queryKey: ["me"], queryFn: api.me, staleTime: 5 * 60_000, retry: false });

  if (me.isPending) {
    return (
      <div className="grid min-h-dvh place-items-center text-muted" role="status">
        <span className="flex items-center gap-2 text-sm">
          <Spinner /> Loading…
        </span>
      </div>
    );
  }
  if (me.isError) {
    return (
      <div className="grid min-h-dvh place-items-center px-4 text-center">
        <div>
          <p className="font-medium text-danger">{me.error.message}</p>
          <Button className="mt-4" onClick={() => router.replace("/login")}>
            Go to sign in
          </Button>
        </div>
      </div>
    );
  }

  const profile = me.data;
  const nav = [
    { href: "/dashboard", label: "Dashboard" },
    { href: "/projects", label: "Projects" },
    ...(profile.role === "Admin" ? [{ href: "/admin", label: "Users" }] : []),
  ];

  async function signOut() {
    await api.logout();
    qc.clear();
    router.replace("/login");
  }

  return (
    <ProfileContext.Provider value={profile}>
      <header className="sticky top-0 z-30 border-b border-line bg-bg/90 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2 sm:h-14 sm:flex-nowrap sm:gap-6 sm:px-6 sm:py-0">
          <Link href="/dashboard" className="text-lg font-semibold tracking-tight">
            ProjectTracker<span className="text-accent">.</span>
          </Link>

          <nav aria-label="Primary" className="order-last -mx-1 flex w-full min-w-0 gap-1 overflow-x-auto sm:order-none sm:w-auto sm:flex-1">
            {nav.map((item) => {
              const active = pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  className={`rounded-lg px-3 py-1.5 text-sm whitespace-nowrap transition-colors ${
                    active ? "bg-accent-soft font-medium text-ink" : "text-muted hover:text-ink"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>

          <div className="ml-auto flex shrink-0 items-center gap-2 sm:ml-0">
            <span className="hidden items-center gap-2 text-sm text-muted md:flex">
              {profile.email}
              <Badge tone={profile.role === "Admin" ? "ok" : "neutral"}>{profile.role}</Badge>
            </span>
            <ThemeToggle />
            <Button size="sm" variant="ghost" onClick={signOut}>
              Sign out
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">{children}</main>
    </ProfileContext.Provider>
  );
}
