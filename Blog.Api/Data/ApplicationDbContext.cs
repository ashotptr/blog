using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Blog.Api.Models;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace Blog.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<BlogPost> BlogPosts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            if (Database.IsSqlite())
            {
                var converter = new ValueConverter<List<string>, string>(
                    tags => JsonSerializer.Serialize(tags, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null)
                            ?? new List<string>());

                var comparer = new ValueComparer<List<string>>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    tags => tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
                    tags => tags.ToList());

                builder.Entity<BlogPost>()
                       .Property(post => post.Tags)
                       .HasConversion(converter, comparer)
                       .HasColumnType("TEXT")
                       .HasDefaultValue(new List<string>());
            }
        }
    }
}
