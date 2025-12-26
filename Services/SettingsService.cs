using FinDesk.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinDesk.Services;

public static class SettingsService
{
    private static string GetAppDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinDesk");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string SettingsPath => Path.Combine(GetAppDir(), "settings.json");

    public static async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            var s = NewDefaults();
            await SaveAsync(s);
            return s;
        }

        var json = await File.ReadAllTextAsync(SettingsPath, Encoding.UTF8);
        var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? NewDefaults();

        if (string.IsNullOrWhiteSpace(settings.DataDir))
            settings.DataDir = GetAppDir();

        if (string.IsNullOrWhiteSpace(settings.DbPath))
            settings.DbPath = Path.Combine(settings.DataDir, "finance.sqlite");

        return settings;
    }

    public static async Task SaveAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(SettingsPath, json, Encoding.UTF8);
    }

    public static string Protect(string plain)
    {
        if (string.IsNullOrWhiteSpace(plain)) return "";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch { return ""; }
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64)) return "";
        try
        {
            var protectedBytes = Convert.FromBase64String(protectedBase64);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    private static AppSettings NewDefaults()
    {
        var dir = GetAppDir();
        return new AppSettings
        {
            DataDir = dir,
            DbPath = Path.Combine(dir, "finance.sqlite")
        };
    }
}
