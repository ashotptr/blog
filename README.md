# Blog
 
Full-stack blog platform. **React 19 + TypeScript + Vite** frontend,
**ASP.NET Core 8** API with Identity, JWT auth with rotating refresh tokens,
**PostgreSQL** via EF Core, **Redis** caching. Posts are written in Markdown.
 
Published through a Cloudflare Tunnel.
 
## Features
 
- Post list with excerpts, tags, and case-insensitive search across titles,
  bodies, and tags
- Markdown rendering with syntax highlighting
- Short-lived access tokens with 7-day rotating refresh tokens
- Roles (Admin / Writer / Reader)
- `GET /api/blogposts` served from Redis, 5-minute expiry, if Redis is unreachable the API logs a warning and reads from Postgres
- Rate limiting on every auth endpoint
- Migrations applied on startup
## Layout
 
```
Blog.Api/          ASP.NET Core 8 API controllers, EF Core, TokenService
blog-client/       React + Vite SPA, served by nginx in production
deploy/            Production compose stack behind a Cloudflare Tunnel
docker-compose.yml Local stack
```
 
## Run it
 
```bash
docker compose up -d --build
```
 
- Site: <http://localhost:8080>
- Sign in: `admin@localhost.dev` / `AdminDev04!`

Migrations run and a sample post is seeded on first start. `docker compose down`
stops it, data persists in the `pgdata` volume, `down -v` wipes it.
 
## Deployment
 
`deploy/docker-compose.yml` runs Postgres, Redis, the API, the client behind
nginx, and a `cloudflared` connector.
