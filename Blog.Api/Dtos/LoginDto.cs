namespace Blog.Api.Dtos;

using System.ComponentModel.DataAnnotations;
public class LoginDto 
{ 
    [Required] public string? Username { get; set; } 
    [Required] public string? Password { get; set; } 
}
