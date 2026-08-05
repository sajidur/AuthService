using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface ICampaignService
    {
        Task<Campaign> CreateCampaignAsync(Campaign campaign, Guid applicationId);
        Task<IEnumerable<Campaign>> GetCampaignsAsync(Guid applicationId);
        Task<Campaign> GetCampaignByIdAsync(string id, Guid applicationId);
        Task UpdateCampaignAsync(string id, Campaign campaign, Guid applicationId);
        Task DeleteCampaignAsync(string id, Guid applicationId);
        Task<Campaign> SendCampaignAsync(string id, Guid applicationId);
        Task<Campaign> CancelScheduleAsync(string id, Guid applicationId);
        Task<IEnumerable<Campaign>> GetDueScheduledCampaignsAsync();
    }

    public class CampaignService : ICampaignService
    {
        private readonly ICampaignRepository _campaignRepository;
        private readonly ISubscriberService _subscriberService;
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly IContactRepository _contactRepository;

        public CampaignService(
            ICampaignRepository campaignRepository,
            ISubscriberService subscriberService,
            IUserService userService,
            IEmailService emailService,
            IContactRepository contactRepository)
        {
            _campaignRepository = campaignRepository;
            _subscriberService = subscriberService;
            _userService = userService;
            _emailService = emailService;
            _contactRepository = contactRepository;
        }

        private async Task<Dictionary<string, Contact>> GetContactsByEmailAsync(Guid applicationId)
        {
            var contacts = await _contactRepository.FindAsync(c => c.ApplicationId == applicationId);
            return contacts
                .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                .GroupBy(c => c.Email.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());
        }

        private static bool IsFutureSchedule(DateTime? scheduledDate) =>
            scheduledDate.HasValue && scheduledDate.Value > DateTime.UtcNow;

        public async Task<Campaign> CreateCampaignAsync(Campaign campaign, Guid applicationId)
        {
            campaign.ApplicationId = applicationId;
            campaign.Status = IsFutureSchedule(campaign.ScheduledDate) ? "scheduled" : "draft";
            await _campaignRepository.AddAsync(campaign);
            return campaign;
        }

        public async Task<IEnumerable<Campaign>> GetCampaignsAsync(Guid applicationId)
        {
            var all = await _campaignRepository.GetAllAsync();
            return all.Where(c => c.ApplicationId == applicationId);
        }

        public async Task<Campaign> GetCampaignByIdAsync(string id, Guid applicationId)
        {
            var campaign = await _campaignRepository.GetByIdAsync(new Guid(id));
            if (campaign == null || campaign.ApplicationId != applicationId)
                return null;
            return campaign;
        }

        public async Task UpdateCampaignAsync(string id, Campaign campaign, Guid applicationId)
        {
            var existing = await GetCampaignByIdAsync(id, applicationId);
            if (existing == null) return;

            existing.Name = campaign.Name;
            existing.Subject = campaign.Subject;
            existing.Body = campaign.Body;
            existing.TargetGroup = campaign.TargetGroup;
            existing.VerifiedOnly = campaign.VerifiedOnly;
            existing.ScheduledDate = campaign.ScheduledDate;

            // Only draft/scheduled campaigns are still editable, so re-derive status
            // from the (possibly changed) schedule; sending/completed/failed are left alone.
            if (existing.Status == "draft" || existing.Status == "scheduled")
            {
                existing.Status = IsFutureSchedule(existing.ScheduledDate) ? "scheduled" : "draft";
            }

            await _campaignRepository.UpdateAsync(existing);
        }

        public async Task DeleteCampaignAsync(string id, Guid applicationId)
        {
            var campaign = await GetCampaignByIdAsync(id, applicationId);
            if (campaign != null)
            {
                await _campaignRepository.RemoveAsync(campaign);
            }
        }

        public async Task<Campaign> CancelScheduleAsync(string id, Guid applicationId)
        {
            var campaign = await GetCampaignByIdAsync(id, applicationId);
            if (campaign == null || campaign.Status != "scheduled")
                return campaign;

            campaign.ScheduledDate = null;
            campaign.Status = "draft";
            await _campaignRepository.UpdateAsync(campaign);
            return campaign;
        }

        public async Task<IEnumerable<Campaign>> GetDueScheduledCampaignsAsync()
        {
            var now = DateTime.UtcNow;
            return await _campaignRepository.FindAsync(c =>
                c.Status == "scheduled" && c.ScheduledDate != null && c.ScheduledDate <= now);
        }

        public async Task<Campaign> SendCampaignAsync(string id, Guid applicationId)
        {
            var campaign = await GetCampaignByIdAsync(id, applicationId);
            if (campaign == null) return null;

            if (campaign.Status != "draft" && campaign.Status != "scheduled")
                return campaign;

            List<EmailRecipient> recipients;
            if (!string.IsNullOrWhiteSpace(campaign.TargetGroup) && campaign.TargetGroup != "all")
            {
                var users = await _userService.GetUsersByGroupAsync(campaign.TargetGroup, applicationId);
                recipients = users
                    .Where(u => !campaign.VerifiedOnly || u.EmailConfirmed)
                    .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                    .Select(u => new EmailRecipient { Email = u.Email, FirstName = u.FirstName, LastName = u.LastName })
                    .ToList();
            }
            else
            {
                var subscribers = await _subscriberService.GetSubscribersAsync(applicationId.ToString());
                var contactsByEmail = await GetContactsByEmailAsync(applicationId);
                recipients = subscribers
                    .Where(s => !campaign.VerifiedOnly || s.IsVerified)
                    .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                    .Select(s =>
                    {
                        contactsByEmail.TryGetValue(s.Email.Trim().ToLowerInvariant(), out var contact);
                        return new EmailRecipient
                        {
                            Email = s.Email,
                            FirstName = contact?.FirstName,
                            LastName = contact?.LastName,
                            Company = contact?.Company
                        };
                    })
                    .ToList();
            }

            campaign.Status = "sending";
            campaign.TotalRecipients = recipients.Count;
            await _campaignRepository.UpdateAsync(campaign);

            if (recipients.Count == 0)
            {
                campaign.Status = "failed";
                campaign.TotalSent = 0;
                campaign.TotalFailed = 0;
                campaign.SentDate = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);
                return campaign;
            }

            var globalVariables = new Dictionary<string, string> { ["campaignName"] = campaign.Name ?? "" };
            var result = await _emailService.SendPersonalizedEmailAsync(applicationId, campaign.Subject, campaign.Body, recipients, globalVariables);

            campaign.Status = result.FailedCount == 0 ? "completed" : result.SentCount == 0 ? "failed" : "completed";
            campaign.TotalSent = result.SentCount;
            campaign.TotalFailed = result.FailedCount;
            campaign.SentDate = DateTime.UtcNow;

            await _campaignRepository.UpdateAsync(campaign);
            return campaign;
        }
    }
}
