namespace SQCScanner.Modal
{
    public class userAuthChecked
    {
        public int id { get; set; }
        public string EmpId { get; set; } = "";
        public string Token { get; set; } = "";
        public string Expiry { get; set; } = "";
        public string role { get; set; } = "";
    }
}