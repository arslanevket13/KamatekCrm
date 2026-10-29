namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Tanılama olaylarının önem derecesi. Serilog'un <c>LogEventLevel</c> tipinden
    /// bağımsızdır; böylece UI ve test katmanı Serilog'a doğrudan bağlanmaz.
    /// </summary>
    public enum DiagnosticLevel
    {
        Verbose = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5
    }
}
