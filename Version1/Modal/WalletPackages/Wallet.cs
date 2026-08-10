using System.ComponentModel.DataAnnotations;

namespace SQCScanner.Modal.Wallet
{
    public class WalletClass
    {
        [Key]
        public int SrId { get; set; }
        public string Uid { get; set; } = "";
        public DateTime TimeDate { get; set; }
        public string CreditLimit { get; set; } = "";
    }
}
