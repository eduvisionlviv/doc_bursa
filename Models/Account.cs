namespace FinDesk.Models
{
    public class Account
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }
}
