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

        public static readonly String[] All = { WindowBorder, ControlHover, ScreenEdge };

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

        public static String DirectoryPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MxHaptics");

        public static String FilePath => Path.Combine(DirectoryPath, "settings.json");

        public ReactionSettings For(String reaction) => reaction switch
        {
            Reactions.WindowBorder => this.WindowBorder ??= new ReactionSettings(),
            Reactions.ControlHover => this.ControlHover ??= new ReactionSettings(),
            Reactions.ScreenEdge => this.ScreenEdge ??= new ReactionSettings(),
            _ => throw new ArgumentOutOfRangeException(nameof(reaction), reaction, "Neznámá reakce"),
        };

        /// <summary>Načte nastavení; při chybějícím nebo poškozeném souboru vrátí výchozí.</summary>
        public static HapticsSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonSerializer.Deserialize<HapticsSettings>(File.ReadAllText(FilePath), JsonOptions);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: nastavení nejde načíst: {ex.Message}");
            }

            return new HapticsSettings();
        }

        /// <summary>Uloží atomicky (zápis do dočasného souboru a přejmenování).</summary>
        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, FilePath, overwrite: true);
        }
    }
}
