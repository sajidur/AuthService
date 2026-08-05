using System;

namespace AuthMicroservice.Model
{
    public class Subscriber : BaseEntity
    {
        public string Email { get; set; }
        public bool IsSubscribed { get; set; }
        public string ApplicationId { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string? VerificationToken { get; set; }
        public DateTime? VerificationTokenExpiry { get; set; }
    }
}
