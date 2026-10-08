# ProjectTracker

A small project-tracking system: an ASP.NET Core REST API with JWT authentication and roles,
plus a Next.js dashboard on top of it.

```
browser ──► web (Next.js) ──► api (ASP.NET Core 10) ──► MySQL
   token in an httpOnly cookie      JWT · roles · EF Core
   demo mode: sample data in the browser, no backend needed
```

## What the API does

- **Auth**: register, login (BCrypt + JWT), profile. Roles `Admin` and `User`.
- **Projects**: CRUD with paging, search, status filter and sorting. Everyone signed in can read;
  only Admins can create, edit or delete.
- **Dashboard**: totals, status counts and average progress, computed by the database.
- **Admin**: list users and change their role.
- **Weather**: a lookup that proxies a third-party service, with its failures mapped to
  404 / 502 / 504 and results cached for ten minutes.
- **Ops**: `/healthz` (process up), `/readyz` (database reachable), rate limiting, CORS allow-list,
  RFC 9457 problem responses everywhere.

### Design decisions worth knowing

- **"Overdue" is derived, never stored.** Only `On Progress` and `Completed` are persisted; a project
  is overdue when it is unfinished and its end date has passed. A stored flag would go stale overnight.
- **Status and progress must agree**: `Completed` requires progress 100, and progress 100 requires `Completed`.
- **Ids in URLs are obfuscated** with Hashids. That hides sequential keys; it is *not* encryption and is
  never a substitute for the role checks.
- **Fail fast configuration.** A JWT secret under 32 characters or a missing `HASHIDS_SALT` stops the app at
  startup instead of failing on the first login. There is no built-in fallback secret or salt.
- **Login timing**: an unknown email costs the same as a wrong password (a dummy BCrypt check), and both
  return the same message.
- **Unique project names** are enforced by the database; a lost race returns 409, not 500.

### Endpoints

| Method | Path | Access |
| --- | --- | --- |
| POST | `/api/auth/register`, `/api/auth/login` | public, rate limited |
| GET | `/api/profile` | signed in |
| GET | `/api/projects?search=&status=&sortBy=&sortOrder=&page=&pageSize=` | signed in |
| GET | `/api/projects/{id}` | signed in |
| POST / PUT / DELETE | `/api/projects`, `/api/projects/{id}` | Admin |
| GET | `/api/dashboard/summary` | signed in |
| GET | `/api/admin/users` · PUT `/api/users/{id}/role` | Admin |
| GET | `/api/weather/{city}` | signed in, rate limited |
| GET | `/healthz` · `/readyz` | public |

`status` is `On Progress`, `Completed` or `Overdue`; `sortBy` is `name`, `status`, `startDate`, `endDate`
or `progress`; `pageSize` is at most 100. A list answers
`{ items, page, pageSize, total, totalPages }`; errors answer `{ title, status, detail }`.

## Run it

Needs Docker.

```bash
cp .env.example .env              # then fill in real values
docker compose up -d --build      # API on http://localhost:5104, MySQL inside the compose network
```

The first start applies the migrations, creates the Admin from `SEED_ADMIN_*` and, unless you turn it off,
adds twelve sample projects. Try `http://localhost:5104/readyz`, then log in with the seeded admin.

Without Docker: install the .NET 10 SDK and MySQL, copy `api/.env.example` to `api/.env`, and run
`dotnet run --project api/API`. Swagger is at `/swagger` in Development.

### Front end

```bash
cd web
cp .env.example .env.local        # set PROJECTTRACKER_API_URL=http://localhost:5104
npm install && npm run dev        # http://localhost:3002
```

Leave `PROJECTTRACKER_API_URL` unset and the app runs in **demo mode**: sample data kept in your browser,
login `admin@demo.test` / `demo1234`. That is what a Vercel deployment uses.

## Configuration

| Key | Meaning |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | MySQL connection string |
| `JwtSettings__Secret / Issuer / Audience / ExpirationInMinutes` | token signing (secret: 32+ chars) |
| `HASHIDS_SALT` | salt for id obfuscation (8+ chars) |
| `Cors__Origins` | comma-separated browser origins allowed to call the API |
| `Database__AutoMigrate` | apply migrations on startup |
| `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`, `SEED_SAMPLE_DATA` | optional start-up data |
| `RateLimiting__AuthPerMinute`, `RateLimiting__WeatherPerMinute` | requests per client address per minute |
| `Https__Redirect` | set `false` behind a TLS proxy or in a container |

## Tests

```bash
dotnet test api/Tests
# or, with no SDK installed:
docker run --rm -v "$PWD/api:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Tests/Tests.csproj
```

76 tests: unit tests for the status rules, id obfuscation and request validation, and integration tests that
host the real app on in-memory SQLite (auth, roles, CRUD, paging and filters, dashboard maths, the weather
failure mapping with a fake provider, CORS, rate limiting, startup validation, seeding). The same behaviour was
also checked end to end against a real MySQL container.

## Changes from the first version

The API is not backwards compatible with the earliest commits: `GET /api/projects` now returns a page object
instead of an array, errors are problem responses instead of `{ Message }`, writes need the Admin role, and the
profile `Id` is obfuscated.
