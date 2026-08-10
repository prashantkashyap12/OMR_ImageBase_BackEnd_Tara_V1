using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Version1.Modal
{
    public class OmrResult
    {
        [Key]
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
        public Dictionary<string, string> FieldResults { get; set; } = new();
        public DateTime ProcessedAt {get; set; } = DateTime.UtcNow;
    }
}
