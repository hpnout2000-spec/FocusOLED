using System;
using System.IO;
using System.Text.Json;
using FocusGlow.Models;

namespace FocusGlow.Services
{
    public sealed class ConfigManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        private static readonly object LockObj = new();
        private static AppConfig? _current;

        public static event Action<AppConfig>? ConfigChanged;

        public static AppConfig Current
        {
            get
            {
                if (_current == null)
                {
                    lock (LockObj)
                    {
                        _current ??= LoadInternal();
                    }
                }
                return _current;
            }
        }

        private static AppConfig LoadInternal()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                        return config;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            }

            var defaultConfig = new AppConfig();
            Save(defaultConfig);
            return defaultConfig;
        }

        public static void Save(AppConfig config)
        {
            lock (LockObj)
            {
                _current = config;
                try
                {
                    string json = JsonSerializer.Serialize(config, JsonOptions);
                    File.WriteAllText(ConfigPath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
                }
            }

            ConfigChanged?.Invoke(config);
        }

        public static void Update(Action<AppConfig> updater)
        {
            var config = Current;
            updater(config);
            Save(config);
        }
    }
}
