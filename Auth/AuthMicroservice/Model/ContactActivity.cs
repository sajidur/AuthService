using System;

namespace AuthMicroservice.Model
{
    public class ContactActivity : BaseEntity
    {
        public Guid ContactId { get; set; }
        public Guid ApplicationId { get; set; }
        public string Type { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
    }
}
