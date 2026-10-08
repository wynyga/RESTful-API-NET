import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";

import { backend } from "@/lib/server/backend";
import { COOKIE } from "@/lib/server/session";

// Forwards /api/<anything> to the ProjectTracker API, adding the session token from the
// httpOnly cookie. /api/auth/login and /api/auth/logout have their own routes (they manage
// the cookie) and take precedence over this catch-all.

type Ctx = { params: Promise<{ path: string[] }> };

async function proxy(req: NextRequest, ctx: Ctx) {
  const { path } = await ctx.params;
  const token = (await cookies()).get(COOKIE)?.value;
  const hasBody = req.method !== "GET" && req.method !== "HEAD";

  const res = await backend(req.method, "/api/" + path.map(encodeURIComponent).join("/"), {
    search: req.nextUrl.search,
    body: hasBody ? await req.text() : undefined,
    token,
  });
  return NextResponse.json(res.json, { status: res.status });
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as DELETE };
