namespace MxHaptics.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Počty skutečně odeslaných vibrací podle reakce: celkem a po dnech (místní datum).
    /// Testy z okna nastavení se nepočítají. Uložené v %LOCALAPPDATA%\MxHaptics\stats.json, zapisuje jen plugin.
    /// Není thread-safe, plugin k ní přistupuje pod zámkem.
    /// </summary>
    public sealed class HapticsStats
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>Den prvního záznamu (yyyy-MM-dd).</summary>
        public String Since { get; set; }

        public Dictionary<String, Int64> Total { get; set; } = new();

        /// <summary>Den (yyyy-MM-dd) → reakce → počet.</summary>
        public Dictionary<String, Dictionary<String, Int64>> Days { get; set; } = new();

        public static String FilePath => Path.Combine(HapticsSettings.DirectoryPath, "stats.json");

        public static String DayKey(DateTime day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        public void Add(String reaction, DateTime now)
        {
            var day = DayKey(now);
            this.Since ??= day;
            this.Total[reaction] = this.TotalOf(reaction) + 1;
            if (!this.Days.TryGetValue(day, out var counts))
            {
                this.Days[day] = counts = new Dictionary<String, Int64>();
            }

            counts[reaction] = counts.GetValueOrDefault(reaction) + 1;
        }

        public Int64 TotalOf(String reaction) => this.Total.GetValueOrDefault(reaction);

        public Int64 DayOf(String reaction, DateTime day) =>
            this.Days.TryGetValue(DayKey(day), out var counts) ? counts.GetValueOrDefault(reaction) : 0;

        public String ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        /// <summary>Při prázdném nebo poškozeném vstupu vrátí prázdnou statistiku.</summary>
        public static HapticsStats FromJson(String json)
        {
            try
            {
                var stats = String.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<HapticsStats>(json, JsonOptions);
                if (stats != null)
                {
                    stats.Total ??= new();
                    stats.Days ??= new();
                    return stats;
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: statistika nejde přečíst: {ex.Message}");
            }

            return new HapticsStats();
        }

        public static HapticsStats Load()
        {
            try
            {
                return FromJson(File.Exists(FilePath) ? File.ReadAllText(FilePath) : null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: statistika nejde načíst: {ex.Message}");
                return new HapticsStats();
            }
        }

        /// <summary>Uloží atomicky (zápis do dočasného souboru a přejmenování).</summary>
        public void Save()
        {
            Directory.CreateDirectory(HapticsSettings.DirectoryPath);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, this.ToJson());
            File.Move(tmp, FilePath, overwrite: true);
        }
    }
}
