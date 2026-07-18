namespace Blog.Api.Dtos;

/// <summary>
/// Public read models. Posts are never returned as raw entities because the
/// Author navigation is an Identity user — serialising it would leak the
/// password hash, security stamp, and refresh token.
/// </summary>
public class PostSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class PostDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public List<string> Tags { get; set; } = new();
}
