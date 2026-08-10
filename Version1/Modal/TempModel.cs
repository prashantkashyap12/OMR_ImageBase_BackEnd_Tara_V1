using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal
{
    public class TempRecord
    {
        [Key]
        public int Id { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }
}
