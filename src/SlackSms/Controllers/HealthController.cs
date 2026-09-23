using Microsoft.AspNetCore.Mvc;

namespace SlackSms.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Content("ok");
}
