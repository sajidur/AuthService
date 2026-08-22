using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailVerificationController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        private readonly IVerificationService _verificationService;

        public EmailVerificationController(IApplicationService applicationService, IVerificationService verificationService)
        {
            _applicationService = applicationService;
            _verificationService = verificationService;
        }

        // Triggered from the logged-in admin UI (resend/bulk-resend verification), not by
        // the public — requires a valid session.
        [Authorize]
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendVerificationRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Type))
                return BadRequest(new { message = "Email and Type are required." });

            var sent = await _verificationService.GenerateAndSendAsync(app.Id, request.Email, request.Type);
            if (!sent)
                return NotFound(new { message = "No matching record found for that email." });

            return Ok(new { message = "Verification email sent." });
        }

        [Authorize]
        [HttpPost("send-bulk")]
        public async Task<IActionResult> SendBulk([FromBody] SendBulkVerificationRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (string.IsNullOrWhiteSpace(request?.Type))
                return BadRequest(new { message = "Type is required." });

            var count = await _verificationService.SendBulkAsync(app.Id, request.Type);
            return Ok(new { message = $"Verification emails sent to {count} recipients.", count });
        }

        // Hit anonymously from the verification link in an email — the requester has no
        // session at this point, only the one-time token in the request body.
        [AllowAnonymous]
        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm([FromBody] ConfirmVerificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Token) || string.IsNullOrWhiteSpace(request?.Type))
                return BadRequest(new { message = "Token and Type are required." });

            var confirmed = await _verificationService.ConfirmAsync(request.Token, request.Type);
            if (!confirmed)
                return BadRequest(new { message = "This verification link is invalid or has expired." });

            return Ok(new { message = "Email verified successfully." });
        }

        private async Task<(bool, Application)> IsValidAppKey(string appKey)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);
            if (app == null) return (false, null);

            // [Authorize] has already validated the JWT itself; this confirms the token's
            // own tenant (ApplicationId claim) matches the AppKey-resolved tenant for this
            // request, so a valid token for tenant A can't be replayed against tenant B's
            // AppKey.
            var applicationIdClaim = User.FindFirst("ApplicationId")?.Value;
            var isValid = !string.IsNullOrEmpty(applicationIdClaim) && applicationIdClaim == app.Id.ToString();
            return (isValid, app);
        }
    }

    public class SendVerificationRequest
    {
        public string Email { get; set; }
        public string Type { get; set; }
    }

    public class SendBulkVerificationRequest
    {
        public string Type { get; set; }
    }

    public class ConfirmVerificationRequest
    {
        public string Token { get; set; }
        public string Type { get; set; }
    }
}
