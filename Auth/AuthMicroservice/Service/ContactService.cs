using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public class BulkImportResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
    }

    public interface IContactService
    {
        Task<Contact> CreateContactAsync(Contact contact, Guid applicationId);
        Task<IEnumerable<Contact>> GetContactsAsync(Guid applicationId);
        Task<Contact> GetContactByIdAsync(string id, Guid applicationId);
        Task UpdateContactAsync(string id, Contact contact, Guid applicationId);
        Task DeleteContactAsync(string id, Guid applicationId);
        Task<BulkImportResult> BulkImportContactsAsync(List<Contact> rows, Guid applicationId);
    }

    public class ContactService : IContactService
    {
        private readonly IContactRepository _contactRepository;

        public ContactService(IContactRepository contactRepository)
        {
            _contactRepository = contactRepository;
        }

        public async Task<Contact> CreateContactAsync(Contact contact, Guid applicationId)
        {
            var existingContact = await _contactRepository.GetByEmailAndApplicationIdAsync(contact.Email, applicationId);
            if (existingContact != null)
            {
                // Decide how to handle duplicates, e.g., throw an exception or update existing.
                throw new System.Exception("A contact with this email already exists.");
            }

            contact.ApplicationId = applicationId;
            await _contactRepository.AddAsync(contact);
            return contact;
        }

        public async Task<IEnumerable<Contact>> GetContactsAsync(Guid applicationId)
        {
            var allContacts = await _contactRepository.GetAllAsync();
            return allContacts.Where(c => c.ApplicationId == applicationId);
        }

        public async Task<Contact> GetContactByIdAsync(string id, Guid applicationId)
        {
            var contact = await _contactRepository.GetByIdAsync(new Guid(id));
            if (contact == null || contact.ApplicationId != applicationId)
            {
                return null;
            }
            return contact;
        }

        public async Task UpdateContactAsync(string id, Contact contact, Guid applicationId)
        {
            var existingContact = await GetContactByIdAsync(id, applicationId);
            if (existingContact == null)
            {
                // Or handle as a "not found" case.
                return;
            }

            // Update properties
            existingContact.FirstName = contact.FirstName;
            existingContact.LastName = contact.LastName;
            existingContact.Email = contact.Email;
            existingContact.Phone = contact.Phone;
            existingContact.Company = contact.Company;
            existingContact.JobTitle = contact.JobTitle;
            existingContact.Department = contact.Department;
            existingContact.TopicsOfInterest = contact.TopicsOfInterest;
            existingContact.Source = contact.Source;
            existingContact.Notes = contact.Notes;
            existingContact.Category = contact.Category;
            existingContact.Grade = contact.Grade;
            existingContact.Industry = contact.Industry;
            existingContact.Country = contact.Country;
            existingContact.City = contact.City;
            existingContact.TimeZone = contact.TimeZone;
            existingContact.IsSubscribed = contact.IsSubscribed;
            existingContact.SubscribedDate = contact.SubscribedDate;
            existingContact.UnsubscribedDate = contact.UnsubscribedDate;
            existingContact.EngagementScore = contact.EngagementScore;
            existingContact.LastInteractionDate = contact.LastInteractionDate;
            existingContact.PreferredLanguage = contact.PreferredLanguage;
            existingContact.Status = contact.Status;

            await _contactRepository.UpdateAsync(existingContact);
        }

        public async Task DeleteContactAsync(string id, Guid applicationId)
        {
            var contact = await GetContactByIdAsync(id, applicationId);
            if (contact != null)
            {
                await _contactRepository.RemoveAsync(contact);
            }
        }

        public async Task<BulkImportResult> BulkImportContactsAsync(List<Contact> rows, Guid applicationId)
        {
            var result = new BulkImportResult();

            var existingByEmail = (await _contactRepository.FindAsync(c => c.ApplicationId == applicationId))
                .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                .GroupBy(c => c.Email.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            var toCreate = new List<Contact>();
            var seenInBatch = new HashSet<string>();

            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Email))
                {
                    result.Skipped++;
                    continue;
                }

                var key = row.Email.Trim().ToLowerInvariant();

                if (existingByEmail.TryGetValue(key, out var existing))
                {
                    MergeContactFields(existing, row);
                    await _contactRepository.UpdateAsync(existing);
                    result.Updated++;
                }
                else if (!seenInBatch.Add(key))
                {
                    // Duplicate email within the same import file.
                    result.Skipped++;
                }
                else
                {
                    row.Id = Guid.NewGuid();
                    row.ApplicationId = applicationId;
                    row.Email = row.Email.Trim();
                    toCreate.Add(row);
                }
            }

            if (toCreate.Count > 0)
            {
                await _contactRepository.AddRangeAsync(toCreate);
                result.Created = toCreate.Count;
            }

            return result;
        }

        private static void MergeContactFields(Contact existing, Contact incoming)
        {
            existing.FirstName = FirstNonEmpty(incoming.FirstName, existing.FirstName);
            existing.LastName = FirstNonEmpty(incoming.LastName, existing.LastName);
            existing.Phone = FirstNonEmpty(incoming.Phone, existing.Phone);
            existing.Company = FirstNonEmpty(incoming.Company, existing.Company);
            existing.JobTitle = FirstNonEmpty(incoming.JobTitle, existing.JobTitle);
            existing.Department = FirstNonEmpty(incoming.Department, existing.Department);
            existing.TopicsOfInterest = FirstNonEmpty(incoming.TopicsOfInterest, existing.TopicsOfInterest);
            existing.Source = FirstNonEmpty(incoming.Source, existing.Source);
            existing.Notes = FirstNonEmpty(incoming.Notes, existing.Notes);
            existing.Status = FirstNonEmpty(incoming.Status, existing.Status);
            existing.Category = FirstNonEmpty(incoming.Category, existing.Category);
            existing.Grade = FirstNonEmpty(incoming.Grade, existing.Grade);
            existing.Industry = FirstNonEmpty(incoming.Industry, existing.Industry);
            existing.Country = FirstNonEmpty(incoming.Country, existing.Country);
            existing.City = FirstNonEmpty(incoming.City, existing.City);
            existing.TimeZone = FirstNonEmpty(incoming.TimeZone, existing.TimeZone);
            existing.PreferredLanguage = FirstNonEmpty(incoming.PreferredLanguage, existing.PreferredLanguage);
            if (incoming.EngagementScore.HasValue)
                existing.EngagementScore = incoming.EngagementScore;
        }

        private static string FirstNonEmpty(string incoming, string current) =>
            string.IsNullOrWhiteSpace(incoming) ? current : incoming;
    }
}
