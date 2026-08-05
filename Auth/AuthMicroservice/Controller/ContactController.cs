using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly IApplicationService _applicationService;
        private readonly IContactActivityService _contactActivityService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;

        public ContactController(
            IContactService contactService,
            IApplicationService applicationService,
            IContactActivityService contactActivityService,
            IEmailService emailService,
            ISmsService smsService)
        {
            _contactService = contactService;
            _applicationService = applicationService;
            _contactActivityService = contactActivityService;
            _emailService = emailService;
            _smsService = smsService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateContact([FromBody] Contact contact, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdContact = await _contactService.CreateContactAsync(contact, app.Id);
            return CreatedAtAction(nameof(GetContact), new { id = createdContact.Id }, createdContact);
        }

        [HttpGet]
        public async Task<IActionResult> GetContacts([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contacts = await _contactService.GetContactsAsync(app.Id);
            return Ok(contacts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetContact(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            return Ok(contact);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateContact(string id, [FromBody] Contact contact, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (id != contact.Id.ToString())
                return BadRequest();

            contact.ApplicationId = app.Id;
            await _contactService.UpdateContactAsync(id, contact, app.Id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContact(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            await _contactService.DeleteContactAsync(id, app.Id);
            return NoContent();
        }

        [HttpGet("{id}/activities")]
        public async Task<IActionResult> GetContactActivities(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            var activities = await _contactActivityService.GetActivitiesForContactAsync(contactId, app.Id);
            return Ok(activities);
        }

        [HttpPost("{id}/send-email")]
        public async Task<IActionResult> SendContactEmail(string id, [FromBody] ContactEmailRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(request?.Subject) || string.IsNullOrWhiteSpace(request?.Body))
                return BadRequest(new { message = "Subject and body are required." });

            var status = await _emailService.SendEmailAsync(app.Id, request.Subject, request.Body, new List<string> { contact.Email });
            return Ok(new { status });
        }

        [HttpPost("{id}/send-sms")]
        public async Task<IActionResult> SendContactSms(string id, [FromBody] ContactSmsRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(contact.Phone))
                return BadRequest(new { message = "Contact has no phone number on file." });

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { message = "Message is required." });

            var sent = await _smsService.SendSmsAsync(contact.Phone, request.Message);
            var status = sent ? "Sent" : "Failed";
            await _contactActivityService.LogActivityForContactAsync(app.Id, contactId, "Sms", "SMS", request.Message, status);
            return Ok(new { status });
        }

        [HttpPost("{id}/log-activity")]
        public async Task<IActionResult> LogContactActivity(string id, [FromBody] ContactLogActivityRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { message = "Message is required." });

            var type = string.IsNullOrWhiteSpace(request.Type) ? "Note" : request.Type;
            await _contactActivityService.LogActivityForContactAsync(app.Id, contactId, type, request.Subject ?? type, request.Message, "Logged");
            return Ok();
        }

        [HttpPost("bulk-import")]
        public async Task<IActionResult> BulkImportContacts([FromBody] List<Contact> contacts, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (contacts == null || contacts.Count == 0)
                return BadRequest(new { message = "No contacts provided." });

            if (contacts.Count > 2000)
                return BadRequest(new { message = "Batch too large; import in chunks of 2000 or fewer." });

            var result = await _contactService.BulkImportContactsAsync(contacts, app.Id);
            return Ok(result);
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

    public class ContactEmailRequest
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public class ContactSmsRequest
    {
        public string Message { get; set; }
    }

    public class ContactLogActivityRequest
    {
        public string Type { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
    }
}
