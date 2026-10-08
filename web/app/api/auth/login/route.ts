import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";

import { backend } from "@/lib/server/backend";
import { COOKIE } from "@/lib/server/session";

// Logs in against the API, then keeps the token in an httpOnly cookie so page scripts
// (and anything injected into them) can never read it. The browser only learns "ok".
export async function POST(req: NextRequest) {
  const res = await backend("POST", "/api/auth/login", { body: await req.text() });
  if (res.status !== 200) return NextResponse.json(res.json, { status: res.status });

  const { token, expiresIn } = res.json as { token: string; expiresIn: number };
  (await cookies()).set(COOKIE, token, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: expiresIn,
  });
  return NextResponse.json({ ok: true });
}
