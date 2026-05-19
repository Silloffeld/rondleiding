using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TemplateJwtProject.Constants;
using TemplateJwtProject.Models;
using TemplateJwtProject.Models.DTOs;
using TemplateJwtProject.Services;

namespace TemplateJwtProject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    [HttpPost("register")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }

        // Wijs standaard de "Admin" rol toe
        await _userManager.AddToRoleAsync(user, Roles.Admin);

        _logger.LogInformation(
            "User {Email} created successfully with role {Role}",
            SanitizeForLog(model.Email),
            Roles.Admin);

        var token = await _jwtService.GenerateTokenAsync(user);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);
        var roles = await _userManager.GetRolesAsync(user);
        
        return Ok(new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken.Token,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToList(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        
        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var token = await _jwtService.GenerateTokenAsync(user);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);
        var roles = await _userManager.GetRolesAsync(user);

        _logger.LogInformation(
            "User {Email} logged in successfully with roles: {Roles}",
            SanitizeForLog(model.Email),
            string.Join(", ", roles));

        return Ok(new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken.Token,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToList(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var refreshToken = await _refreshTokenService.ValidateRefreshTokenAsync(model.RefreshToken);

        if (refreshToken == null)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        var user = refreshToken.User;
        
        // Revoke het oude refresh token
        await _refreshTokenService.RevokeRefreshTokenAsync(
            refreshToken.Token, 
            "Replaced by new token"
        );

        // Genereer nieuwe tokens
        var newAccessToken = await _jwtService.GenerateTokenAsync(user);
        var newRefreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id);
        var roles = await _userManager.GetRolesAsync(user);

        _logger.LogInformation("Refresh token used for user {Email}", SanitizeForLog(user.Email));

        return Ok(new AuthResponseDto
        {
            Token = newAccessToken,
            RefreshToken = newRefreshToken.Token,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToList(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }

    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _refreshTokenService.RevokeRefreshTokenAsync(model.RefreshToken, "Revoked by user");

        _logger.LogInformation("Refresh token revoked");

        return Ok(new { message = "Token revoked successfully" });
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutFromAllDevices()
    {
        var userId = _userManager.GetUserId(User);
        
        if (userId == null)
        {
            return Unauthorized();
        }

        await _refreshTokenService.RevokeAllUserRefreshTokensAsync(userId);

        _logger.LogInformation("User {UserId} logged out from all devices", SanitizeForLog(userId));

        return Ok(new { message = "Logged out from all devices successfully" });
    }

    private static string SanitizeForLog(string? value)
    {
        return value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;
    }
}
