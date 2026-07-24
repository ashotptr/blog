using Microsoft.AspNetCore.Identity;
using Blog.Api.Models;

namespace Blog.Api.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

            string[] roleNames = { "Admin", "Writer", "Reader" };
            
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var adminEmail = configuration["AdminUser:Email"];
            var adminPassword = configuration["AdminUser:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("AdminUser:Email / AdminUser:Password are not configured — skipping admin and demo post seeding.");
                
                return;
            }

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                };

                IdentityResult result = await userManager.CreateAsync(adminUser, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    logger.LogWarning("Could not create admin user: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                    
                    return;
                }
            }

            if (!context.BlogPosts.Any())
            {
                context.BlogPosts.AddRange(DemoPosts.Select(p => new BlogPost
                {
                    Title = p.title,
                    Content = p.content,
                    Tags = p.tags,
                    AuthorId = adminUser.Id,
                    PublishedDate = DateTime.UtcNow.AddDays(p.daysAgo * -1)
                }));

                await context.SaveChangesAsync();
                
                logger.LogInformation("Seeded {Count} demo posts.", DemoPosts.Count);
            }
        }

        private static readonly List<(string title, List<string> tags, int daysAgo, string content)> DemoPosts = new()
        {
            ("Hello, world — why this blog exists", new List<string> { "meta" }, 14,
@"This site is a working sample of a full-stack setup I built from scratch: a **React 19 + TypeScript** frontend, a **.NET 8** API with ASP.NET Core Identity, JWT access tokens with rotating refresh tokens, PostgreSQL via EF Core, and Redis caching — all deployed from a home machine through a Cloudflare Tunnel.

Posts here are written in Markdown and rendered with syntax highlighting, like so:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseNpgsql(connectionString));
```

The source for both the API and this client is on my GitHub — every feature you see here maps to real code you can read."),

            ("JWT refresh tokens: the bug that silently breaks everything", new List<string> { "dotnet", "auth" }, 7,
@"A refresh endpoint usually validates the *expired* access token, looks up the user, and rotates the refresh token. Mine kept returning `400 Invalid token` — and the reason is a classic.

`principal.Identity.Name` is populated from `ClaimTypes.Name`. My token service issued `sub`, `jti`, and `nameidentifier` claims… but never `Name`:

```csharp
new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
```

One line. The lesson: when auth fails *silently*, print the claims collection before anything else. The JWT claim-type outbound mapping (`ClaimTypes.Name` → `unique_name`) is also why the frontend reads `unique_name` from the decoded token."),

            ("Serving a side project from your desk with Cloudflare Tunnel", new List<string> { "devops", "docker" }, 2,
@"You don't need a VPS to put a project on a real domain. `cloudflared` opens an *outbound* connection from your machine to Cloudflare's edge, so nothing on your network is exposed — no port forwarding, no static IP.

The whole stack runs in Docker Compose: Postgres, Redis, the API, the static frontend behind nginx, and a `cloudflared` container:

```yaml
cloudflared:
  image: cloudflare/cloudflared:latest
  command: tunnel run
  environment:
    - TUNNEL_TOKEN=${TUNNEL_TOKEN}
```

Public hostnames map straight to compose service names (`http://blog-client:80`), TLS terminates at Cloudflare's edge, and the laptop just… serves the internet.")
        };
    }
}
