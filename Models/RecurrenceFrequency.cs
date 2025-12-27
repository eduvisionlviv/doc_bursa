using System.Text.Json.Serialization;

namespace doc_bursa.Models
{
    /// <summary>
    /// Описує періодичність рекурентних транзакцій.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RecurrenceFrequency
    {
        Daily,
        Weekly,
        Monthly,
        Quarterly,
        Yearly
    }
}

