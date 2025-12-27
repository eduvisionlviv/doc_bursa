using System.Text.Json.Serialization;

namespace FinDesk.Models
{
    /// <summary>
    /// Частота бюджетного періоду.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BudgetFrequency
    {
        Monthly,
        Quarterly,
        Yearly,
        Weekly
    }
}
