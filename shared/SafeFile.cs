namespace MxHaptics.Shared
{
    using System;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// Čtení a zápis souborů v %LOCALAPPDATA%\MxHaptics, které píšou dva procesy (plugin a okno nastavení)
    /// a které může krátce držet antivir nebo zálohovací nástroj.
    /// </summary>
    internal static class SafeFile
    {
        private const Int32 Attempts = 3;
        private const Int32 RetryDelayMs = 30;

        /// <summary>Obsah souboru, nebo null, když neexistuje. Zamčený soubor zkusí přečíst znovu, pak výjimku propustí.</summary>
        public static String ReadAllTextOrNull(String path)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
                catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < Attempts)
                {
                    Thread.Sleep(RetryDelayMs);
                }
            }
        }

        /// <summary>
        /// Zapíše atomicky: dočasný soubor s PID (ať se plugin a okno nepřepisují) a přejmenování přes cíl.
        /// Zamčený cíl (čtenář ho drží; Windows pak hlásí IOException i UnauthorizedAccessException) zkusí znovu,
        /// pak výjimku propustí a dočasný soubor smaže.
        /// </summary>
        public static void WriteAllTextAtomic(String path, String text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, text);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(tmp, path, overwrite: true);
                    return;
                }
                catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < Attempts)
                {
                    Thread.Sleep(RetryDelayMs);
                }
                catch
                {
                    TryDelete(tmp);
                    throw;
                }
            }
        }

        private static void TryDelete(String path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine($"MxHaptics: dočasný soubor nejde smazat: {ex.Message}");
            }
        }
    }
}
