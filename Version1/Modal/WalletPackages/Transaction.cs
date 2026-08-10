using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SQCScanner.Modal
{
    public class Transaction
    {
        [Key]
        public int SrId { get; set; }

        [Required]
        public DateTime TranDate { get; set; } 

        [Required]
        [MaxLength]
        public string TranId { get; set; } = "";

        [Required]
        [StringLength(10)]
        public string EmpId { get; set; } = "";

        [Required]
        public int PackNo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
    }
}
