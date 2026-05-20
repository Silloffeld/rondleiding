using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateJwtProject.Constants;

namespace TemplateJwtProject.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class TestController : ControllerBase
{
    [HttpGet("admin")]
    public IActionResult AdminEndpoint()
    {
        return Ok(new { message = "This endpoint is only accessible by Admins", user = User.Identity?.Name });
    }
}
