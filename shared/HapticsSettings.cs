namespace MxHaptics.Shared
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public enum Strength
    {
        Weak,
        Medium,
        Strong,
    }

    /// <summary>Reakce (zdroje vibrací), které plugin umí hlásit.</summary>
    public static class Reactions
    {
        public const String WindowBorder = "windowBorder";
        public const String ControlHover = "controlHover";
        public const String ScreenEdge = "screenEdge";
        public const String Click = "click";

        public static readonly String[] All = { WindowBorder, ControlHover, ScreenEdge, Click };

        /// <summary>Název haptické události pro reakci a sílu, např. <c>windowBorder_weak</c>.</summary>
        public static String EventName(String reaction, Strength strength) =>
            $"{reaction}_{strength.ToString().ToLowerInvariant()}";
    }

    public sealed class ReactionSettings
    {
        public Boolean Enabled { get; set; } = true;

        public Strength Strength { get; set; } = Strength.Medium;
    }

    /// <summary>
    /// Nastavení sdílené pluginem a oknem nastavení. Uložené v %LOCALAPPDATA%\MxHaptics\settings.json.
    /// </summary>
    public sealed class HapticsSettings
    {
        public const String PipeName = "MxHaptics";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        public Boolean Enabled { get; set; } = true;

        public ReactionSettings WindowBorder { get; set; } = new() { Strength = Strength.Weak };

        public ReactionSettings ControlHover { get; set; } = new() { Strength = Strength.Medium };

        public ReactionSettings ScreenEdge { get; set; } = new() { Strength = Strength.Medium };

        public ReactionSettings Click { get; set; } = new() { Strength = Strength.Medium };

        /// <summary>Reakce <see cref="Reactions.ControlHover"/> i na tlačítkách hlavní lišty Windows (UI Automation).</summary>
        public Boolean TaskbarButtons { get; set; } = true;

        public static String DirectoryPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MxHaptics");

        public static String FilePath => Path.Combine(DirectoryPath, "settings.json");

        public ReactionSettings For(String reaction) => reaction switch
        {
            Reactions.WindowBorder => this.WindowBorder ??= new ReactionSettings(),
            Reactions.ControlHover => this.ControlHover ??= new ReactionSettings(),
            Reactions.ScreenEdge => this.ScreenEdge ??= new ReactionSettings(),
            Reactions.Click => this.Click ??= new ReactionSettings(),
            _ => throw new ArgumentOutOfRangeException(nameof(reaction), reaction, "Neznámá reakce"),
        };

        /// <summary>Načte nastavení; když soubor chybí nebo nejde přečíst, vrátí výchozí.</summary>
        public static HapticsSettings Load() => TryLoad(out var settings) ? settings : new HapticsSettings();

        /// <summary>
        /// Načte nastavení. Chybějící soubor = výchozí nastavení (true). False = soubor existuje, ale nejde přečíst
        /// (zamčený i po opakování, poškozený): volající si má nechat svůj stav, ne ho nahradit výchozím.
        /// </summary>
        public static Boolean TryLoad(out HapticsSettings settings)
        {
            try
            {
                var json = SafeFile.ReadAllTextOrNull(FilePath);
                settings = (json == null ? null : JsonSerializer.Deserialize<HapticsSettings>(json, JsonOptions)) ?? new HapticsSettings();
                settings.Normalize();
                return true;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: nastavení nejde načíst: {ex.Message}");
                settings = null;
                return false;
            }
        }

        /// <summary>Uloží atomicky; souběžný zápis pluginu a okna se nepřepisuje (viz <see cref="SafeFile"/>).</summary>
        public void Save() => SafeFile.WriteAllTextAtomic(FilePath, JsonSerializer.Serialize(this, JsonOptions));

        /// <summary>Síla mimo enum (ručně upravený JSON, číslo) by dala neexistující událost: vrátí výchozí.</summary>
        private void Normalize()
        {
            var defaults = new HapticsSettings();
            foreach (var reaction in Reactions.All)
            {
                var rs = this.For(reaction);
                if (!Enum.IsDefined(rs.Strength))
                {
                    rs.Strength = defaults.For(reaction).Strength;
                }
            }
        }
    }
}
