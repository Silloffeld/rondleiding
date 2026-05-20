using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TemplateJwtProject.Constants;
using TemplateJwtProject.Models;
using TemplateJwtProject.Models.DTOs;

namespace TemplateJwtProject.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("assign-role")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Valideer of de rol bestaat
        if (model.Role != Roles.Admin)
        {
            return BadRequest(new { message = $"Invalid role. Valid role is: {Roles.Admin}" });
        }

        if (await _userManager.IsInRoleAsync(user, model.Role))
        {
            return BadRequest(new { message = $"User already has the {model.Role} role" });
        }

        var result = await _userManager.AddToRoleAsync(user, model.Role);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to assign role", errors = result.Errors });
        }

        _logger.LogInformation("Admin assigned role {Role} to user {Email}", model.Role, model.Email);

        var roles = await _userManager.GetRolesAsync(user);

        // Check if it's the admin's first login
        if (model.Role == Roles.Admin && !user.PasswordChanged)
        {
            return Forbid();
        }

        return Ok(new 
        { 
            message = $"Role {model.Role} assigned successfully",
            email = user.Email,
            roles = roles
        });
    }
    [HttpPost("force-password-change")]
    public async Task<IActionResult> ForcePasswordChange([FromBody] AssignRoleDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Set the PasswordChanged flag to true
        user.PasswordChanged = true;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to update user", errors = result.Errors });
        }

        _logger.LogInformation("Admin forced password change for user {Email}", model.Email);

        return Ok(new 
        { 
            message = $"Password changed successfully",
            email = user.Email
        });
    }


    [HttpPost("remove-role")]
    public async Task<IActionResult> RemoveRole([FromBody] AssignRoleDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        if (model.Role != Roles.Admin)
        {
            return BadRequest(new { message = $"Invalid role. Valid role is: {Roles.Admin}" });
        }

        if (!await _userManager.IsInRoleAsync(user, model.Role))
        {
            return BadRequest(new { message = $"User does not have the {model.Role} role" });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, model.Role);
        
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to remove role", errors = result.Errors });
        }

        _logger.LogInformation("Admin removed role {Role} from user {Email}", model.Role, model.Email);

        var roles = await _userManager.GetRolesAsync(user);
        
        return Ok(new 
        { 
            message = $"Role {model.Role} removed successfully",
            email = user.Email,
            roles = roles
        });
    }

    [HttpGet("admins")]
    public async Task<IActionResult> GetAllAdmins()
    {
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);

        var adminList = new List<object>();

        foreach (var admin in admins)
        {
            var roles = await _userManager.GetRolesAsync(admin);
            adminList.Add(new
            {
                id = admin.Id,
                email = admin.Email,
                userName = admin.UserName,
                roles = roles
            });
        }

        return Ok(adminList);
    }
}
