using System;
using System.ComponentModel.DataAnnotations;

namespace AuthMicroservice.Model
{
    public class WhatsappConfig : BaseEntity
    {
        [Required]
        public string BusinessAccountId { get; set; }
        [Required]
        public string PhoneNumberId { get; set; }
        [Required]
        public string DisplayPhoneNumber { get; set; }
        [Required]
        public string AccessToken { get; set; }
        public string ConnectionStatus { get; set; } = "connected";

        [Required]
        public Guid ApplicationId { get; set; }
    }
}
