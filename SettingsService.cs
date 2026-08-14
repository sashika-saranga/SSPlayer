using System;
using System.IO;
using System.Text.Json;

namespace Mp3Player
{
    public static class SettingsService
    {
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SSmp3Player");
        private static readonly string PathFile = System.IO.Path.Combine(Folder, "userSettings.json");

        public static UserSettings Load()
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                if (!File.Exists(PathFile))
                {
                    var def = new UserSettings();
                    Save(def);
                    return def;
                }
                var txt = File.ReadAllText(PathFile);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var obj = JsonSerializer.Deserialize<UserSettings>(txt, opts);
                return obj ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        public static void Save(UserSettings settings)
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var txt = JsonSerializer.Serialize(settings, opts);
                File.WriteAllText(PathFile, txt);
            }
            catch { }
        }
    }
}
