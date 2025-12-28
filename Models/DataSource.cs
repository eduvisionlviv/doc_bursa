using System;
using System.Collections.Generic;

namespace doc_bursa.Models
{
    public class DataSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DateTime? LastSync { get; set; }
        public string PingStatus { get; set; } = string.Empty;
                public string Provider { get; set; } = string.Empty;
        public List<DiscoveredAccount> DiscoveredAccounts { get; set; } = new();
    }
}
