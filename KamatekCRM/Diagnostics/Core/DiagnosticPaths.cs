using System;
using System.IO;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Tanılama dosyalarının tek merkezden yönetilen yolları.
    /// Yollar kullanıcı girdisinden türetilmez; sabit kök altında tutulur.
    /// </summary>
    public static class DiagnosticPaths
    {
        public static string Root { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KamatekCRM");

        public static string Logs => Path.Combine(Root, "Logs");

        public static string Diagnostics => Path.Combine(Root, "Diagnostics");

        public static string Crashes => Path.Combine(Diagnostics, "Crashes");

        public static string Exports => Path.Combine(Diagnostics, "Exports");

        /// <summary>Dizini (yoksa) oluşturur ve aynı yolu döndürür.</summary>
        public static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
