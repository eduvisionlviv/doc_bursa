namespace FinDesk.Models
{
    public class DataSource
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DateTime? LastSync { get; set; }
    }
}
