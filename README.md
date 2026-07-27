# Dev Blog — full-stack personal blog

A personal developer blog built end-to-end: **React 19 + TypeScript + Vite** on
the front, **ASP.NET Core 8** on the back, with ASP.NET Core Identity,
**JWT auth with rotating refresh tokens**, optional **Google sign-in**,
role-based authorization (Admin / Writer / Reader), **PostgreSQL** via EF Core,
**Redis** read-through caching, and API rate limiting. Posts are written in
Markdown and rendered with syntax highlighting.

## Features

- Public post list with excerpts, tags, and case-insensitive search across
  titles, bodies, and tags
- Markdown post pages with fenced-code syntax highlighting (PrismLight build —
  only the languages a dev blog needs are bundled)
- Sign in with username/password or Google (ID-token validation server-side —
  no OAuth middleware, no client secret on the server)
- 15-minute access tokens, 7-day rotating refresh tokens with a sliding window,
  transparent refresh in an axios interceptor (single-flight: parallel 401s
  share one refresh request)
- Role-aware UI: Writers/Admins get a dashboard, an editor with Markdown
  preview, and edit/delete on their own posts (Admins on everything)
- `GET /api/blogposts` served read-through from Redis with 5-minute expiry and
  invalidation on every mutation; a dead Redis degrades to DB reads instead of
  taking the site down
- Fixed-window rate limiting on every auth endpoint
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
deploy/              Production compose stack behind a Cloudflare Tunnel
```

## Run it locally

**With Docker — one command, nothing else installed:**

```bash
docker compose up -d --build
# client   http://localhost:8080
# API      http://localhost:5028/swagger
# sign in  admin@localhost.dev / AdminDev04!
```

Migrations run on first start and a dev admin plus three demo posts are seeded.
`docker compose down` stops it; data persists in the `pgdata` volume.

**Without Docker** you need the .NET 8 SDK, Node 22, and PostgreSQL 16. Redis is
optional — the API falls back to an in-memory cache when no Redis connection
string is set.

**1. Start the database.** The dev configuration
(`appsettings.Development.json`) expects PostgreSQL on `localhost:5432` with
database `blogdb` and user/password `postgres`/`postgres`:

```bash
docker run -d --name blog-postgres -e POSTGRES_DB=blogdb -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 -v blog-pgdata:/var/lib/postgresql/data postgres:16-alpine
```

Without a reachable database the API retries five times on startup and then
exits with a connection error.

**2. Run the API:**

```bash
cd Blog.Api
dotnet run
# Swagger at http://localhost:5028/swagger
# A dev admin (admin@localhost.dev / AdminDev04!) and three demo posts
# are seeded on first start.
```

**3. Run the client:**

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
| `Jwt__Key` | HMAC signing key — at least 32 bytes, **required** |
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

The live site runs from a home machine. `deploy/docker-compose.yml` brings up
Postgres, Redis, the API, the static client behind nginx, and a `cloudflared`
connector that publishes it on a domain — no port forwarding, no static IP, no
exposed ports. `cloudflared` opens an *outbound* connection to Cloudflare's
edge and TLS terminates there.

**1. Create the tunnel.** Cloudflare dashboard → Zero Trust → Networks →
Tunnels → Create a tunnel → Cloudflared. Copy the connector token (the long
string after `--token`); nothing needs installing locally, the stack runs the
connector itself.

**2. Map the hostnames.** In the tunnel's *Public Hostnames* tab add two
routes. The service URLs are compose service names, resolved over the internal
Docker network:

| Public hostname          | Service                 |
| ------------------------ | ----------------------- |
| `blog.<your-domain>`     | `http://blog-client:80` |
| `api.blog.<your-domain>` | `http://blog-api:8080`  |

**3. Start it.**

```bash
cd deploy
cp .env.example .env
openssl rand -base64 48   # paste as JWT_KEY
# fill in DOMAIN, TUNNEL_TOKEN, POSTGRES_PASSWORD, ADMIN_EMAIL, ADMIN_PASSWORD
docker compose up -d --build
```

Every required variable is guarded, so a missing one fails the `up` immediately
with a message naming it rather than starting a half-broken stack. First start
applies migrations and seeds the roles, your admin account, and three demo
posts.

For Google sign-in, set `GOOGLE_CLIENT_ID` in `.env` — the client id is
compiled into the frontend bundle, so rebuild both services:
`docker compose up -d --build blog-client blog-api`.

```bash
docker compose logs -f blog-api      # tail a service
docker compose up -d --build         # redeploy after code changes
docker compose down                  # stop; data persists in the pgdata volume

docker compose exec postgres pg_dump -U bloguser blogdb > backup.sql
docker compose exec -T postgres psql -U bloguser blogdb < backup.sql
```

Dumps are gitignored (`*.sql`) — they contain user emails and Identity password
hashes.