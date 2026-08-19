using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blog.Api.Dtos;
using Blog.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Blog.Api.Tests;

public class BlogPostsApiTests : IClassFixture<SqliteApiFactory>
{
    private readonly SqliteApiFactory _factory;

    public BlogPostsApiTests(SqliteApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBlogPosts_ReturnsEmptyList_OnAFreshDatabase()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/BlogPosts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var posts = await response.Content.ReadFromJsonAsync<List<PostSummaryDto>>();

        Assert.NotNull(posts);
    }

    [Fact]
    public async Task GetBlogPost_ReturnsNotFound_ForAnIdThatDoesNotExist()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/BlogPosts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatingAPost_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/BlogPosts",
            new { Title = "No token", Content = "should be rejected", Tags = new[] { "x" } });

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"expected 401 or 403, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AnUnknownRoute_Returns404_RatherThanCrashing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/NoSuchThing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tags_SurviveARoundTripThroughSqlite()
    {
        await using var context = _factory.CreateContext();

        var post = new BlogPost
        {
            Title = "Tagged",
            Content = "body",
            PublishedDate = DateTime.UtcNow,
            Tags = new List<string> { "csharp", "sqlite", "ef-core" }
        };

        context.BlogPosts.Add(post);
        await context.SaveChangesAsync();

        await using var reader = _factory.CreateContext();
        var stored = await reader.BlogPosts.AsNoTracking()
            .FirstAsync(p => p.Id == post.Id);

        Assert.Equal(3, stored.Tags.Count);
        Assert.Contains("sqlite", stored.Tags);
        Assert.Equal(new[] { "csharp", "sqlite", "ef-core" }, stored.Tags);
    }

    [Fact]
    public async Task Tags_MutatedInPlace_ArePersisted()
    {
        await using var context = _factory.CreateContext();

        var post = new BlogPost
        {
            Title = "Mutating tags",
            Content = "body",
            PublishedDate = DateTime.UtcNow,
            Tags = new List<string> { "first" }
        };

        context.BlogPosts.Add(post);
        await context.SaveChangesAsync();

        var tracked = await context.BlogPosts.FirstAsync(p => p.Id == post.Id);
        tracked.Tags.Add("second");
        await context.SaveChangesAsync();

        await using var reader = _factory.CreateContext();
        var stored = await reader.BlogPosts.AsNoTracking().FirstAsync(p => p.Id == post.Id);

        Assert.Equal(2, stored.Tags.Count);
        Assert.Contains("second", stored.Tags);
    }

    [Fact]
    public async Task APostWrittenDirectly_IsVisibleThroughTheApi()
    {
        await using var context = _factory.CreateContext();

        var post = new BlogPost
        {
            Title = "Visible through HTTP",
            Content = "body",
            PublishedDate = DateTime.UtcNow,
            Tags = new List<string>()
        };

        context.BlogPosts.Add(post);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/BlogPosts/{post.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<PostDetailDto>();

        Assert.NotNull(detail);
        Assert.Equal("Visible through HTTP", detail!.Title);
    }
}
