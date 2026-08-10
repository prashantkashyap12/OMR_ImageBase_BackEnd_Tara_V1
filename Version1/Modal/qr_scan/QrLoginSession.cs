using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sentry;

namespace YourProject.Models
{
    public class QrLoginSession
    {
        public int Id { get; set; }
        public Guid SessionId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public bool Used { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int? UserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}