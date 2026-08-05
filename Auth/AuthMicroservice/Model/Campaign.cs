using System;

namespace AuthMicroservice.Model
{
    public class Campaign : BaseEntity
    {
        public string Name { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; } = string.Empty;
        public string Status { get; set; } = "draft";
        public string TargetGroup { get; set; } = "all";
        public bool VerifiedOnly { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? SentDate { get; set; }
        public Guid ApplicationId { get; set; }
        public int TotalRecipients { get; set; }
        public int TotalSent { get; set; }
        public int TotalFailed { get; set; }
    }
}
