using System;
using System.IO;
using System.Text.Json;

namespace SavagePadEmu;

public sealed class ProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool TryLoadProfile(string path, out Profile? profile, out Exception? error)
    {
        profile = null;
        error = null;
        try
        {
            if (!File.Exists(path)) return false;
            profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path));
            return profile is not null;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    public bool TryLoadSettings(string path, out AppSettings? settings)
    {
        settings = null;
        try
        {
            if (!File.Exists(path)) return false;
            settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            return settings is not null;
        }
        catch
        {
            return false;
        }
    }

    public void SaveProfile(string path, Profile profile) => WriteAtomic(path, JsonSerializer.Serialize(profile, JsonOptions));
    public void SaveSettings(string path, AppSettings settings) => WriteAtomic(path, JsonSerializer.Serialize(settings, JsonOptions));

    public void EnsureDefaultProfile(string path)
    {
        if (!File.Exists(path))
            SaveProfile(path, DefaultProfileFactory.Create());
    }

    private static void WriteAtomic(string path, string contents)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, contents);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
