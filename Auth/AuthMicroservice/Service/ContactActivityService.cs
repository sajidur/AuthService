using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IContactActivityService
    {
        Task LogActivityAsync(Guid applicationId, string email, string type, string subject, string message, string status);
        Task LogActivityForContactAsync(Guid applicationId, Guid contactId, string type, string subject, string message, string status);
        Task<IEnumerable<ContactActivity>> GetActivitiesForContactAsync(Guid contactId, Guid applicationId);
    }

    public class ContactActivityService : IContactActivityService
    {
        private const int SubjectMaxLength = 300;
        private const int MessageMaxLength = 500;

        private readonly IContactActivityRepository _contactActivityRepository;
        private readonly IContactRepository _contactRepository;

        public ContactActivityService(IContactActivityRepository contactActivityRepository, IContactRepository contactRepository)
        {
            _contactActivityRepository = contactActivityRepository;
            _contactRepository = contactRepository;
        }

        public async Task LogActivityAsync(Guid applicationId, string email, string type, string subject, string message, string status)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            var contact = await _contactRepository.GetByEmailAndApplicationIdAsync(email.Trim(), applicationId);
            if (contact == null) return;

            await SaveActivityAsync(contact, applicationId, type, subject, message, status);
        }

        public async Task LogActivityForContactAsync(Guid applicationId, Guid contactId, string type, string subject, string message, string status)
        {
            var contact = await _contactRepository.GetByIdAsync(contactId);
            if (contact == null || contact.ApplicationId != applicationId) return;

            await SaveActivityAsync(contact, applicationId, type, subject, message, status);
        }

        private async Task SaveActivityAsync(Contact contact, Guid applicationId, string type, string subject, string message, string status)
        {
            var activity = new ContactActivity
            {
                ContactId = contact.Id,
                ApplicationId = applicationId,
                Type = type,
                Subject = Truncate(subject, SubjectMaxLength),
                Message = Truncate(StripHtml(message), MessageMaxLength),
                Status = status,
            };

            await _contactActivityRepository.AddAsync(activity);

            var now = DateTime.UtcNow;
            contact.LastInteractionDate = now;
            if (status == "Sent" || status == "Received")
            {
                var normalizedType = type?.ToLowerInvariant();
                if (normalizedType == "email") contact.LastEmailDate = now;
                else if (normalizedType == "sms") contact.LastSmsDate = now;
                else if (normalizedType == "facebook-message") contact.LastFacebookMessageDate = now;
                else if (normalizedType == "whatsapp-message") contact.LastWhatsappDate = now;
            }
            await _contactRepository.UpdateAsync(contact);
        }

        public async Task<IEnumerable<ContactActivity>> GetActivitiesForContactAsync(Guid contactId, Guid applicationId)
        {
            var activities = await _contactActivityRepository.FindAsync(a => a.ContactId == contactId && a.ApplicationId == applicationId);
            return activities.OrderByDescending(a => a.CreatedDate);
        }

        private static string StripHtml(string input) =>
            string.IsNullOrEmpty(input) ? input : Regex.Replace(input, "<.*?>", " ").Trim();

        private static string Truncate(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var collapsed = Regex.Replace(input, @"\s+", " ").Trim();
            return collapsed.Length <= maxLength ? collapsed : collapsed.Substring(0, maxLength) + "…";
        }
    }
}
