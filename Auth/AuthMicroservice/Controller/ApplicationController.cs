using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuthMicroservice.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class ApplicationController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        private readonly IWebHostEnvironment _environment;

        public ApplicationController(IApplicationService applicationService, IWebHostEnvironment environment)
        {
            _applicationService = applicationService;
            _environment = environment;
        }

        // Debug/support tooling — dumps every tenant's AppKey/AppSecret, so it must never
        // be reachable outside Development regardless of auth. The old hardcoded
        // "pass11" query-string password has been removed; [Authorize] is the real gate.
        [Authorize]
        [HttpGet("AllApp")]
        public async Task<IActionResult> AllApp()
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            var app = await _applicationService.GetAllAsync();

            return Ok(app);
        }

        // Tenant provisioning isn't self-serve today — nothing in the frontend calls this.
        // Same as AllApp: [Authorize] + Development-only until a real provisioning flow
        // (and a platform-admin concept, since regular tenant users shouldn't be able to
        // mint new tenants) is designed.
        [Authorize]
        [HttpPost("register")]
        public async Task<IActionResult> RegisterApplication([FromBody] ApplicationRequest application)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (string.IsNullOrEmpty(application.Name))
            {
                return BadRequest("Application name cannot be null or empty.");
            }

            var app = await _applicationService.RegisterApplicationAsync(application);

            return Ok(app);
        }

        [AllowAnonymous]
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateApplication([FromBody] Application app)
        {
            // Validate the application key and secret asynchronously
            if (await _applicationService.ValidateAppKeyAndSecretAsync(app.AppKey, app.AppSecret))
            {
                return Ok();
            }
            return Unauthorized();
        }




    }

}
