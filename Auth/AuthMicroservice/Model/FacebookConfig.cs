using System;
using System.ComponentModel.DataAnnotations;

namespace AuthMicroservice.Model
{
    public class FacebookConfig : BaseEntity
    {
        [Required]
        public string PageId { get; set; }
        [Required]
        public string PageName { get; set; }
        [Required]
        public string PageAccessToken { get; set; }
        public string ConnectionStatus { get; set; } = "connected";

        [Required]
        public Guid ApplicationId { get; set; }
    }
}
