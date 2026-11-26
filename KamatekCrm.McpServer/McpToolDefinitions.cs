using System.Text.Json.Nodes;

namespace KamatekCrm.McpServer;

/// <summary>
/// MCP tools/list yanıtı için 6 aracın JSON Schema tanımlarını üretir.
/// </summary>
internal static class McpToolDefinitions
{
    public static JsonArray CreateAll() => new()
    {
        BuildTool(
            "launch_or_attach_app",
            "127.0.0.1:19876/health yanıt veriyorsa çalışan KamatekCRM Debug örneğine bağlanır; aksi halde KamatekCrm.exe (Debug) sürecini başlatıp hazır olana dek 20 sn bekler.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["exePath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Opsiyonel KamatekCrm.exe tam yolu. Boş bırakılırsa çözüm kökünden otomatik bulunur."
                    },
                    ["arguments"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Opsiyonel komut satırı argümanları (örn. --first-run)."
                    },
                    ["timeoutSeconds"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Sağlık kontrolü bekleme süresi (varsayılan: 20 sn)."
                    }
                }
            }),

        BuildTool(
            "navigate_and_theme",
            "KamatekCRM içinde belirtilen modül rotasına geçiş yapar ve/veya aktif temayı (PremiumLight / MidnightDark) değiştirir.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["route"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Hedef ekran: Dashboard, Customers, Interactions, ManagerAgenda, ServiceJobs, Repairs, Quotations, DirectSales, Stock, Finance, FinancialHealth, Analytics, Purchasing, Suppliers, StockCount, Reports, Users, SystemLogs, Settings, Login."
                    },
                    ["theme"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Opsiyonel tema adı: PremiumLight, MidnightDark veya Glassmorphism."
                    }
                }
            }),

        BuildTool(
            "get_ui_tree",
            "Aktif pencere (veya en üstteki modal) üzerindeki görünür etkileşimli WPF kontrollerini ve uyarı/hata banner metinlerini kompakt JSON dizisi olarak döndürür.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["maxNodes"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Döndürülecek maksimum öğe sayısı (varsayılan: 120, maks: 500)."
                    }
                }
            }),

        BuildTool(
            "invoke_action",
            "Hedef WPF kontrolü üzerinde click, type, select veya aktif pencerede press_key kısayol aksiyonunu yürütür.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["action"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("click", "type", "select", "press_key"),
                        ["description"] = "Yürütülecek etkileşim türü."
                    },
                    ["target"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "get_ui_tree çıktısındaki refId (örn. e1), AutomationId veya görünür etiket metni."
                    },
                    ["value"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Yazılacak metin (type), seçilecek öğe/indeks (select) veya kısayol tuşu (press_key: Esc, Enter, Ctrl+K, Ctrl+N, Alt+Left, F12)."
                    }
                },
                ["required"] = new JsonArray("action")
            }),

        BuildTool(
            "capture_screenshot",
            "Aktif KamatekCRM penceresinin ekran görüntüsünü PNG olarak kaydeder ve hem dosya yolunu hem image/png base64 verisini döndürür.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["filename"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Opsiyonel PNG dosya adı (örn. customers_dark.png)."
                    }
                }
            }),

        BuildTool(
            "get_diagnostics",
            "Bellek içi WPF DataBinding/Resource hatalarını, Serilog Warning/Error/Fatal kayıtlarını, aktif View/ViewModel adını ve IsBusy durumunu döndürür.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["clear"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "True verilirse okuma sonrasında bellek içi hata tamponlarını temizler."
                    }
                }
            })
    };

    private static JsonObject BuildTool(string name, string description, JsonObject inputSchema) => new()
    {
        ["name"] = name,
        ["description"] = description,
        ["inputSchema"] = inputSchema
    };
}
