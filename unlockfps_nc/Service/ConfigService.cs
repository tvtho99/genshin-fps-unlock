using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using unlockfps_nc.Model;

namespace unlockfps_nc.Service
{
    public class ConfigService
    {
        private readonly string _configPath;
        private object _lock = new();

        public Config Config { get; private set; } = new();

        public ConfigService()
            : this(Path.Combine(AppContext.BaseDirectory, "fps_config.json"))
        {
        }

        internal ConfigService(string configPath)
        {
            _configPath = configPath;
            Load();
            Sanitize();
        }

        private void Load()
        {
            if (!File.Exists(_configPath))
                return;

            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<Config>(json);

                if (config != null)
                    Config = config;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                BackupConfig();
            }
        }

        private void BackupConfig()
        {
            try
            {
                File.Copy(_configPath, _configPath + ".bak", true);
            }
            catch (IOException)
            {
                // Keep the defaults even if the file cannot be backed up.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the defaults if the backup is blocked by file permissions.
            }
        }

        private void Sanitize()
        {
            Config.DllList ??= new();
            Config.GamePath ??= "";
            Config.AdditionalCommandLine ??= "";
            Config.FPSTarget = Math.Clamp(Config.FPSTarget, 10, 1000);
            Config.Priority = Math.Clamp(Config.Priority, 0, 5);
            Config.CustomResX = Math.Clamp(Config.CustomResX, 200, 7680);
            Config.CustomResY = Math.Clamp(Config.CustomResY, 200, 4320);
            Config.MonitorNum = Math.Clamp(Config.MonitorNum, 1, 100);
        }

        public void Save()
        {
            lock (_lock) {
                var configPath = _configPath;
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });

                bool wasHidden = false;
                if (File.Exists(configPath)) {
                    var attributes = File.GetAttributes(configPath);
                    if ((attributes & FileAttributes.Hidden) != 0) {
                        wasHidden = true;
                        File.SetAttributes(configPath, attributes & ~FileAttributes.Hidden);
                    }
                }

                try {
                    using var fs = new FileStream(configPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
                    using var sw = new StreamWriter(fs, Encoding.UTF8);
                    sw.Write(json);
                }
                finally {
                    if (wasHidden && File.Exists(configPath)) {
                        var attributes = File.GetAttributes(configPath);
                        File.SetAttributes(configPath, attributes | FileAttributes.Hidden);
                    }
                }
            }

        }

    }
}
