using cashfree_pg.Model;

namespace SQCScanner.Modal.PGatway
{
    public class CreateOrderModel
    {
        public decimal Order_Amount { get; set; }
        public CustomerDetails Customer_Details { get; set; }
    }
    public class CustomerDetails
    {
        public string Customer_Name { get; set; }
        public string Customer_Email { get; set; }
        public string Customer_Phone { get; set; }
    }
}
