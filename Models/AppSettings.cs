namespace FinDesk.Models;

public sealed class AppSettings
{
    public string DataDir { get; set; } = "";
    public string DbPath { get; set; } = "";

    // Secrets are stored encrypted (Windows DPAPI). If decryption fails -> treat as empty.
    public string MonoTokenProtected { get; set; } = "";

    // Optional / Advanced fields (carcass)
    public string PrivatTokenProtected { get; set; } = "";
    public string PrivatClientId { get; set; } = "";
    public string PrivatBaseUrl { get; set; } = "https://acp.privatbank.ua";

    public string UkrsibCertificatePath { get; set; } = "";
    public string UkrsibClientId { get; set; } = "";
    public string UkrsibBaseUrl { get; set; } = "https://api.ukrsibbank.com";
}
