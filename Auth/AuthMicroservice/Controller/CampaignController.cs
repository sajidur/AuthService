using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;
        private readonly IApplicationService _applicationService;

        public CampaignController(ICampaignService campaignService, IApplicationService applicationService)
        {
            _campaignService = campaignService;
            _applicationService = applicationService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateCampaign([FromBody] Campaign campaign, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _campaignService.CreateCampaignAsync(campaign, app.Id);
            return CreatedAtAction(nameof(GetCampaign), new { id = created.Id }, created);
        }

        [HttpGet]
        public async Task<IActionResult> GetCampaigns([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaigns = await _campaignService.GetCampaignsAsync(app.Id);
            return Ok(campaigns);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCampaign(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaign = await _campaignService.GetCampaignByIdAsync(id, app.Id);
            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCampaign(string id, [FromBody] Campaign campaign, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (id != campaign.Id.ToString())
                return BadRequest();

            await _campaignService.UpdateCampaignAsync(id, campaign, app.Id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCampaign(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            await _campaignService.DeleteCampaignAsync(id, app.Id);
            return NoContent();
        }

        [HttpPost("{id}/send")]
        public async Task<IActionResult> SendCampaign(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaign = await _campaignService.SendCampaignAsync(id, app.Id);
            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        [HttpPost("{id}/cancel-schedule")]
        public async Task<IActionResult> CancelSchedule(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaign = await _campaignService.CancelScheduleAsync(id, app.Id);
            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        private async Task<(bool, Application)> IsValidAppKey(string appKey)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);
            if (app == null) return (false, null);

            var isValid = await _applicationService.ValidateAppKeyAndSecretAsync(appKey, app.AppSecret);
            return (isValid, app);
        }
    }
}
