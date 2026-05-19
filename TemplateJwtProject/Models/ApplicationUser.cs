using Microsoft.AspNetCore.Identity;

namespace TemplateJwtProject.Models;

public class ApplicationUser : IdentityUser
{
    public bool PasswordChanged { get; set; } = false;
    // Je kunt hier extra properties toevoegen
    // Bijvoorbeeld:
    // public string? FirstName { get; set; }
    // public string? LastName { get; set; }
}
