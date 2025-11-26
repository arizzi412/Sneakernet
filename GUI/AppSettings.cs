using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SneakerNetSync
{
    public class AppSettings
    {
        public string HomeMainPath { get; set; } = "";
        public string HomeUsbPath { get; set; } = "";
        public string OffsiteTargetPath { get; set; } = "";
        public string OffsiteUsbPath { get; set; } = "";

        // NEW: List of exclusion patterns
        public List<string> ExclusionPatterns { get; set; } = new List<string>
        {
            "System Volume Information",
            "$RECYCLE.BIN",
            "*.tmp",
            "Thumbs.db",
            ".git",
            "bin",
            "obj",
            ".vs"
        };

        private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
            }
            catch { /* ignore corruption */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}