# Dev Blog — full-stack personal blog

A personal developer blog built end-to-end: **React 19 + TypeScript + Vite** on
the front, **ASP.NET Core 8** on the back, with ASP.NET Core Identity,
**JWT auth with rotating refresh tokens**, optional **Google sign-in**,
role-based authorization (Admin / Writer / Reader), **PostgreSQL** via EF Core,
**Redis** read-through caching, and API rate limiting. Posts are written in
Markdown and rendered with syntax highlighting.

## Features

- Public post list with excerpts, tags, and full-text search
- Markdown post pages with fenced-code syntax highlighting (PrismLight build —
  only the languages a dev blog needs are bundled)
- Sign in with username/password or Google (ID-token validation server-side —
  no OAuth middleware, no client secret on the server)
- 15-minute access tokens, 7-day rotating refresh tokens with a sliding window,
  transparent refresh in an axios interceptor (single-flight: parallel 401s
  share one refresh request)
- Role-aware UI: Writers/Admins get a dashboard, editor with live Markdown
  preview, edit/delete on their own posts (Admins on everything)
- `GET /api/blogposts` served read-through from Redis with 5-minute expiry and
  invalidation on every mutation; a dead Redis degrades to DB reads instead of
  taking the site down
- Fixed-window rate limiting on all auth endpoints
- EF Core migrations applied automatically on startup (with retry, for
  compose cold starts) + idempotent seeding of roles, the admin account, and
  demo posts

## Project layout

```
Blog.Api/            ASP.NET Core 8 Web API
  Controllers/       Account (register/login/refresh/google), BlogPosts (CRUD + search)
  Data/              DbContext, migrations-on-startup seeding
  Dtos/              Request DTOs + public read models (entities never leave the API)
  Migrations/        EF Core migrations (PostgreSQL)
  Services/          TokenService (JWT + refresh token generation)
blog-client/         React 19 + TypeScript + Vite frontend
  src/api/           axios instance with auth + refresh interceptors
  src/contexts/      AuthContext (typed user decoded from the JWT)
  src/components/    Route guards, Markdown renderer, post card
  src/pages/         List, detail, editor (with preview), dashboard, login, register
```

## Run it locally

You need: .NET 8 SDK, Node 22, PostgreSQL 16 (Redis optional — the API falls
back to an in-memory cache when no Redis connection string is set).

Backend:

```bash
cd Blog.Api
dotnet run
# Swagger at http://localhost:5028/swagger
# Migrations run automatically; a dev admin (admin@localhost.dev / Admin!Dev123)
# and three demo posts are seeded on first start.
```

Frontend:

```bash
cd blog-client
npm install
npm run dev
# http://localhost:5173
```

### Configuration

Everything is configured through standard ASP.NET configuration
(`appsettings.json` → environment variables, `__` as the section separator):

| Key | Purpose |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ConnectionStrings__Redis` | Redis (empty ⇒ in-memory cache) |
| `Jwt__Key` | HMAC signing key — long random string, **required** |
| `Jwt__Issuer` / `Jwt__Audience` | Token issuer/audience (defaults provided) |
| `Cors__AllowedOrigins` | Comma-separated allowed origins |
| `AdminUser__Email` / `AdminUser__Password` | Seeded admin account |
| `Authentication__Google__ClientId` | Enables Google sign-in (optional) |

Client build-time variables: `VITE_API_URL`, `VITE_GOOGLE_CLIENT_ID` (the
Google button renders only when a client id is present).

### Google sign-in setup (optional)

Create an OAuth **Web application** client in Google Cloud Console, add your
site origin to *Authorized JavaScript origins*, and set the same client id in
two places: `Authentication__Google__ClientId` (API) and
`VITE_GOOGLE_CLIENT_ID` (client build). The client secret is **never** needed:
the SPA obtains an ID token and the API validates its signature and audience
with `Google.Apis.Auth`.

> **Security note:** never commit OAuth client secrets or
> `client_secret*.json` files. If a secret has ever been committed, rotate it
> in Google Cloud Console and purge it from git history — `.gitignore` in this
> repo excludes those files.

## Deployment

`Blog.Api/Dockerfile` and `blog-client/Dockerfile` build production images
(the client is a static nginx site with an SPA fallback). A full
Docker Compose stack — Postgres, Redis, both apps, and a Cloudflare Tunnel —
lives in the sibling `deploy/` bundle.
