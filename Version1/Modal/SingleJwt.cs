namespace SQCScanner.Modal
{
    public class SingleJwt
    {
        public int UserId { get; set; }
        public string JwtToken { get; set; }
        public DateTime Expiry { get; set; }
    }
}
