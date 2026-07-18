using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blog.Api.Models
{
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime PublishedDate { get; set; }

        public string? AuthorId { get; set; }

        [ForeignKey("AuthorId")]
        public ApplicationUser? Author { get; set; }

        public List<string> Tags { get; set; } = new List<string>();
    }
}
