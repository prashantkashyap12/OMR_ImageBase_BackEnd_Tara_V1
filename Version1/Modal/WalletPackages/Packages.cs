using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal.Wallet
{
    public class Packages
    {
        [Key]
        public int PackId { get; set; }
        [Required]
        public string PackageName { get; set; } = "";
        public string SubHeading { get; set; } = "";
        [Required]
        public int creditLimit { get; set; }
        [Required]
        public int Amount { get; set; }
    }
}
