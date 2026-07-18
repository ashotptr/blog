using System.Security.Claims;
using Blog.Api.Models;

namespace Blog.Api.Services;
public interface ITokenService 
{ 
    string CreateAccessToken(ApplicationUser user, IList<string> roles); 
    string CreateRefreshToken(); 
}
