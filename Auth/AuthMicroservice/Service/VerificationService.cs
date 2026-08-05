using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IVerificationService
    {
        Task<bool> GenerateAndSendAsync(Guid applicationId, string email, string type);
        Task<int> SendBulkAsync(Guid applicationId, string type);
        Task<bool> ConfirmAsync(string token, string type);
    }

    public class VerificationService : IVerificationService
    {
        private const string TypeUser = "user";
        private const string TypeContact = "contact";
        private const string TypeSubscriber = "subscriber";

        private readonly IUserRepository _userRepository;
        private readonly IContactRepository _contactRepository;
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public VerificationService(
            IUserRepository userRepository,
            IContactRepository contactRepository,
            ISubscriberRepository subscriberRepository,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _contactRepository = contactRepository;
            _subscriberRepository = subscriberRepository;
            _emailService = emailService;
            _configuration = configuration;
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private string BuildVerificationEmail(string link)
        {
            return $@"
<!DOCTYPE html>
<html>
<body style='margin:0; padding:0; font-family: Arial, Helvetica, sans-serif; background-color:#f5f5f5;'>
    <div style='max-width:600px; margin:20px auto; background-color:#ffffff; padding:24px; border-radius:6px;'>
        <h2 style='margin-top:0; color:#333;'>Confirm your email address</h2>
        <p style='color:#555;'>Please confirm this is your email address by clicking the button below.</p>
        <p style='margin:24px 0;'>
            <a href='{link}' style='background-color:#2563eb; color:#ffffff; padding:12px 20px; border-radius:6px; text-decoration:none; font-weight:bold;'>Verify email</a>
        </p>
        <p style='color:#888; font-size:12px;'>If the button doesn't work, copy and paste this link into your browser:<br/>{link}</p>
        <p style='color:#888; font-size:12px;'>This link expires in 24 hours.</p>
    </div>
</body>
</html>";
        }

        private string BuildFrontendLink(string token, string type)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
            return $"{baseUrl}/verify-email?token={Uri.EscapeDataString(token)}&type={Uri.EscapeDataString(type)}";
        }

        public async Task<bool> GenerateAndSendAsync(Guid applicationId, string email, string type)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var token = GenerateToken();
            var expiry = DateTime.UtcNow.AddHours(24);
            var normalizedType = type?.ToLowerInvariant();

            if (normalizedType == TypeUser)
            {
                var user = (await _userRepository.FindAsync(u => u.Email == email && u.ApplicationId == applicationId)).FirstOrDefault();
                if (user == null) return false;

                user.EmailConfirmationToken = token;
                user.EmailConfirmationTokenExpiry = expiry;
                await _userRepository.UpdateAsync(user);
            }
            else if (normalizedType == TypeContact)
            {
                var contact = await _contactRepository.GetByEmailAndApplicationIdAsync(email, applicationId);
                if (contact == null) return false;

                contact.VerificationToken = token;
                contact.VerificationTokenExpiry = expiry;
                await _contactRepository.UpdateAsync(contact);
            }
            else if (normalizedType == TypeSubscriber)
            {
                var subscriber = await _subscriberRepository.GetByEmailAndApplicationIdAsync(email, applicationId.ToString());
                if (subscriber == null) return false;

                subscriber.VerificationToken = token;
                subscriber.VerificationTokenExpiry = expiry;
                await _subscriberRepository.UpdateAsync(subscriber);
            }
            else
            {
                return false;
            }

            var link = BuildFrontendLink(token, normalizedType);
            await _emailService.SendEmailAsync(applicationId, "Verify your email address", BuildVerificationEmail(link), new List<string> { email });
            return true;
        }

        public async Task<int> SendBulkAsync(Guid applicationId, string type)
        {
            var normalizedType = type?.ToLowerInvariant();
            var emails = new List<string>();

            if (normalizedType == TypeContact)
            {
                var contacts = await _contactRepository.FindAsync(c => c.ApplicationId == applicationId && !c.IsVerified);
                emails = contacts.Select(c => c.Email).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            }
            else if (normalizedType == TypeSubscriber)
            {
                var subscribers = await _subscriberRepository.FindAsync(s => s.ApplicationId == applicationId.ToString() && !s.IsVerified);
                emails = subscribers.Select(s => s.Email).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            }
            else
            {
                return 0;
            }

            var sentCount = 0;
            foreach (var email in emails)
            {
                var sent = await GenerateAndSendAsync(applicationId, email, normalizedType);
                if (sent) sentCount++;
            }

            return sentCount;
        }

        public async Task<bool> ConfirmAsync(string token, string type)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var normalizedType = type?.ToLowerInvariant();
            var now = DateTime.UtcNow;

            if (normalizedType == TypeUser)
            {
                var user = (await _userRepository.FindAsync(u => u.EmailConfirmationToken == token)).FirstOrDefault();
                if (user == null || user.EmailConfirmationTokenExpiry == null || user.EmailConfirmationTokenExpiry < now)
                    return false;

                user.EmailConfirmed = true;
                user.EmailConfirmationToken = null;
                user.EmailConfirmationTokenExpiry = null;
                await _userRepository.UpdateAsync(user);
                return true;
            }

            if (normalizedType == TypeContact)
            {
                var contact = (await _contactRepository.FindAsync(c => c.VerificationToken == token)).FirstOrDefault();
                if (contact == null || contact.VerificationTokenExpiry == null || contact.VerificationTokenExpiry < now)
                    return false;

                contact.IsVerified = true;
                contact.VerifiedDate = now;
                contact.VerificationToken = null;
                contact.VerificationTokenExpiry = null;
                await _contactRepository.UpdateAsync(contact);
                return true;
            }

            if (normalizedType == TypeSubscriber)
            {
                var subscriber = (await _subscriberRepository.FindAsync(s => s.VerificationToken == token)).FirstOrDefault();
                if (subscriber == null || subscriber.VerificationTokenExpiry == null || subscriber.VerificationTokenExpiry < now)
                    return false;

                subscriber.IsVerified = true;
                subscriber.VerifiedDate = now;
                subscriber.VerificationToken = null;
                subscriber.VerificationTokenExpiry = null;
                await _subscriberRepository.UpdateAsync(subscriber);
                return true;
            }

            return false;
        }
    }
}
