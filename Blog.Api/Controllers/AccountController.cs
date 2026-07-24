using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Blog.Api.Models;
using Blog.Api.Dtos;
using Blog.Api.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using Google.Apis.Auth;

[Route("api/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AccountController(UserManager<ApplicationUser> userManager, ITokenService tokenService, IConfiguration configuration)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = new ApplicationUser { UserName = registerDto.Username, Email = registerDto.Email };
        var result = await _userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _userManager.AddToRoleAsync(user, "Reader");

        return Ok(new { Message = "User registered successfully!" });
    }

    [HttpPost("login")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByNameAsync(loginDto.Username);

        if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
        {
            return Unauthorized("Invalid credentials");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.CreateAccessToken(user, roles);
        var refreshToken = _tokenService.CreateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _userManager.UpdateAsync(user);

        return Ok(new TokenDto { AccessToken = accessToken, RefreshToken = refreshToken });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> Refresh(TokenDto tokenDto)
    {
        if (tokenDto.AccessToken == null || tokenDto.RefreshToken == null)
        {
            return BadRequest("Invalid client request");
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured.")))
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken securityToken;
        System.Security.Claims.ClaimsPrincipal principal;

        try
        {
            principal = tokenHandler.ValidateToken(tokenDto.AccessToken, tokenValidationParameters, out securityToken);
        }
        catch (SecurityTokenException)
        {
            return BadRequest("Invalid token");
        }

        var jwtSecurityToken = securityToken as JwtSecurityToken;

        if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            return BadRequest("Invalid token");
        }

        var username = principal.Identity?.Name;

        if (username == null)
        {
            return BadRequest("Invalid token");
        }

        var user = await _userManager.FindByNameAsync(username);

        if (user == null || user.RefreshToken != tokenDto.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return BadRequest("Invalid client request");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.CreateAccessToken(user, roles);
        var newRefreshToken = _tokenService.CreateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        return new ObjectResult(new { accessToken = newAccessToken, refreshToken = newRefreshToken });
    }

    [HttpPost("google-login")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> GoogleLogin([FromBody] string credential)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return StatusCode(503, "Google sign-in is not configured on this server.");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings()
        {
            Audience = new List<string> { clientId }
        };

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(credential, settings);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized("Invalid Google credential.");
        }

        var user = await _userManager.FindByEmailAsync(payload.Email);

        if (user == null)
        {
            user = new ApplicationUser { UserName = payload.Email, Email = payload.Email };

            var result = await _userManager.CreateAsync(user);
            
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }
            
            await _userManager.AddToRoleAsync(user, "Reader");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.CreateAccessToken(user, roles);
        var refreshToken = _tokenService.CreateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        return Ok(new TokenDto { AccessToken = accessToken, RefreshToken = refreshToken });
    }
}
