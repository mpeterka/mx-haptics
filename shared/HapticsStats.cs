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
                return Normalize(String.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<HapticsStats>(json, JsonOptions));
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: statistika nejde přečíst: {ex.Message}");
                return new HapticsStats();
            }
        }

        /// <summary>Pro zobrazení (okno): když soubor nejde přečíst, vrátí prázdnou statistiku. Nic nepřepisuje.</summary>
        public static HapticsStats Load() => TryLoad(quarantineCorrupt: false, out var stats) ? stats : new HapticsStats();

        /// <summary>
        /// Načte statistiku. Chybějící soubor = prázdná (true). Zamčený i po opakování = false: volající ho nesmí přepsat,
        /// jinak by zmizela historie. Poškozený JSON s <paramref name="quarantineCorrupt"/> přejmenuje na
        /// <c>stats.json.bad-&lt;čas&gt;</c> a začne od nuly (true); bez něj vrátí false.
        /// </summary>
        public static Boolean TryLoad(Boolean quarantineCorrupt, out HapticsStats stats)
        {
            String json;
            try
            {
                json = SafeFile.ReadAllTextOrNull(FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: statistika nejde načíst: {ex.Message}");
                stats = null;
                return false;
            }

            if (json == null)
            {
                stats = new HapticsStats();
                return true;
            }

            try
            {
                stats = Normalize(JsonSerializer.Deserialize<HapticsStats>(json, JsonOptions));
                return true;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: statistika je poškozená: {ex.Message}");
                stats = null;
                if (!quarantineCorrupt)
                {
                    return false;
                }

                try
                {
                    File.Move(FilePath, $"{FilePath}.bad-{DateTime.Now:yyyyMMdd-HHmmss}");
                }
                catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.WriteLine($"MxHaptics: poškozenou statistiku nejde odložit: {moveEx.Message}");
                    return false;
                }

                stats = new HapticsStats();
                return true;
            }
        }

        /// <summary>Přičte počty z <paramref name="other"/> (soubor, který se napoprvé nepodařilo přečíst).</summary>
        public void MergeFrom(HapticsStats other)
        {
            if (other.Since != null && (this.Since == null || String.CompareOrdinal(other.Since, this.Since) < 0))
            {
                this.Since = other.Since;
            }

            foreach (var (reaction, count) in other.Total)
            {
                this.Total[reaction] = this.TotalOf(reaction) + count;
            }

            foreach (var (day, counts) in other.Days)
            {
                if (!this.Days.TryGetValue(day, out var mine))
                {
                    this.Days[day] = mine = new Dictionary<String, Int64>();
                }

                foreach (var (reaction, count) in counts)
                {
                    mine[reaction] = mine.GetValueOrDefault(reaction) + count;
                }
            }
        }

        /// <summary>Uloží atomicky (viz <see cref="SafeFile"/>).</summary>
        public void Save() => SafeFile.WriteAllTextAtomic(FilePath, this.ToJson());

        private static HapticsStats Normalize(HapticsStats stats)
        {
            stats ??= new HapticsStats();
            stats.Total ??= new();
            stats.Days ??= new();
            return stats;
        }
    }
}
