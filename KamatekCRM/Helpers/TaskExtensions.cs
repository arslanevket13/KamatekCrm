using System;
using System.Threading.Tasks;
using Serilog;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// Fire-and-forget Task çağrılarının istisnalarını sessizce yutmak yerine
    /// yapısal olarak Serilog üzerinden loglayan güvenli yardımcı uzantılar.
    /// </summary>
    public static class TaskExtensions
    {
        public static void SafeFireAndForget(this Task task, string context)
        {
            if (task is null) return;

            _ = ObserveAsync(task, context);
        }

        private static async Task ObserveAsync(Task task, string context)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Arka plan görevi iptal edildi: {Context}", context);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Arka plan görevi başarısız oldu: {Context}", context);
            }
        }
    }
}
