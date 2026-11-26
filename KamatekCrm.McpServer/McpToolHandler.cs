using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KamatekCrm.McpServer;

/// <summary>
/// 6 MCP aracını KamatekCRM Debug HTTP köprüsüne (127.0.0.1:19876) yönlendiren işleyici.
/// Tüm tanılama çıktıları yalnızca Console.Error (stderr) üzerinden yazılır.
/// </summary>
internal sealed class McpToolHandler : IDisposable
{
    private const string BridgeBaseUrl = "http://127.0.0.1:19876";

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(BridgeBaseUrl),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<JsonObject> CallToolAsync(string toolName, JsonElement? arguments, CancellationToken ct)
    {
        try
        {
            return toolName switch
            {
                "launch_or_attach_app" => await LaunchOrAttachAsync(arguments, ct).ConfigureAwait(false),
                "navigate_and_theme" => await NavigateAndThemeAsync(arguments, ct).ConfigureAwait(false),
                "get_ui_tree" => await GetUiTreeAsync(arguments, ct).ConfigureAwait(false),
                "invoke_action" => await InvokeActionAsync(arguments, ct).ConfigureAwait(false),
                "capture_screenshot" => await CaptureScreenshotAsync(arguments, ct).ConfigureAwait(false),
                "get_diagnostics" => await GetDiagnosticsAsync(arguments, ct).ConfigureAwait(false),
                _ => CreateTextResult($"Bilinmeyen MCP aracı: '{toolName}'.", isError: true)
            };
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"[McpToolHandler] Köprü bağlantı hatası ({toolName}): {ex.Message}").ConfigureAwait(false);
            return CreateTextResult(
                $"KamatekCRM Debug köprüsüne (127.0.0.1:19876) ulaşılamadı. Önce 'launch_or_attach_app' aracını çağırın. Detay: {ex.Message}",
                isError: true);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[McpToolHandler] Araç hatası ({toolName}): {ex}").ConfigureAwait(false);
            return CreateTextResult($"Araç yürütme hatası ({toolName}): {ex.Message}", isError: true);
        }
    }

    private async Task<JsonObject> LaunchOrAttachAsync(JsonElement? args, CancellationToken ct)
    {
        string? existingHealth = await TryProbeHealthAsync(ct).ConfigureAwait(false);
        if (existingHealth != null)
        {
            var attachedPayload = new JsonObject
            {
                ["attached"] = true,
                ["launchedNewProcess"] = false,
                ["health"] = TryParseJsonNode(existingHealth) ?? existingHealth
            };
            return CreateTextResult(attachedPayload.ToJsonString(), isError: false);
        }

        string? requestedExe = GetStringArg(args, "exePath");
        string? resolvedExe = ResolveExecutablePath(requestedExe);
        if (string.IsNullOrWhiteSpace(resolvedExe) || !File.Exists(resolvedExe))
        {
            return CreateTextResult(
                $"KamatekCrm.exe bulunamadı ({requestedExe ?? "otomatik arama: KamatekCRM/bin/Debug/net9.0-windows/KamatekCrm.exe"}). Önce Debug derlemesi alın.",
                isError: true);
        }

        string? launchArgs = GetStringArg(args, "arguments");
        int timeoutSeconds = Math.Clamp(GetIntArg(args, "timeoutSeconds", 20), 3, 60);

        var psi = new ProcessStartInfo
        {
            FileName = resolvedExe,
            Arguments = launchArgs ?? string.Empty,
            WorkingDirectory = Path.GetDirectoryName(resolvedExe)!,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return CreateTextResult($"Süreç başlatılamadı: {resolvedExe}", isError: true);
        }

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                var crashPayload = new JsonObject
                {
                    ["attached"] = false,
                    ["crashed"] = true,
                    ["exitCode"] = process.ExitCode,
                    ["exePath"] = resolvedExe
                };
                return CreateTextResult(crashPayload.ToJsonString(), isError: true);
            }

            string? healthJson = await TryProbeHealthAsync(ct).ConfigureAwait(false);
            if (healthJson != null)
            {
                var readyPayload = new JsonObject
                {
                    ["attached"] = true,
                    ["launchedNewProcess"] = true,
                    ["pid"] = process.Id,
                    ["exePath"] = resolvedExe,
                    ["health"] = TryParseJsonNode(healthJson) ?? healthJson
                };
                return CreateTextResult(readyPayload.ToJsonString(), isError: false);
            }

            await Task.Delay(350, ct).ConfigureAwait(false);
        }

        var timeoutPayload = new JsonObject
        {
            ["attached"] = false,
            ["timedOut"] = true,
            ["timeoutSeconds"] = timeoutSeconds,
            ["hasExited"] = process.HasExited,
            ["exitCode"] = process.HasExited ? process.ExitCode : null,
            ["exePath"] = resolvedExe
        };
        return CreateTextResult(timeoutPayload.ToJsonString(), isError: true);
    }

    private async Task<JsonObject> NavigateAndThemeAsync(JsonElement? args, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["route"] = GetStringArg(args, "route"),
            ["theme"] = GetStringArg(args, "theme")
        };

        var (statusCode, responseText) = await PostJsonAsync("/navigate", body.ToJsonString(), ct).ConfigureAwait(false);
        return CreateTextResult(responseText, isError: statusCode >= 400);
    }

    private async Task<JsonObject> GetUiTreeAsync(JsonElement? args, CancellationToken ct)
    {
        int maxNodes = Math.Clamp(GetIntArg(args, "maxNodes", 120), 1, 500);
        var (statusCode, responseText) = await GetRawAsync($"/ui-tree?maxNodes={maxNodes}", ct).ConfigureAwait(false);
        return CreateTextResult(responseText, isError: statusCode >= 400);
    }

    private async Task<JsonObject> InvokeActionAsync(JsonElement? args, CancellationToken ct)
    {
        string? action = GetStringArg(args, "action");
        if (string.IsNullOrWhiteSpace(action))
        {
            return CreateTextResult("'action' parametresi zorunludur (click | type | select | press_key).", isError: true);
        }

        var body = new JsonObject
        {
            ["target"] = GetStringArg(args, "target"),
            ["action"] = action,
            ["value"] = GetStringArg(args, "value")
        };

        var (statusCode, responseText) = await PostJsonAsync("/action", body.ToJsonString(), ct).ConfigureAwait(false);
        return CreateTextResult(responseText, isError: statusCode >= 400);
    }

    private async Task<JsonObject> CaptureScreenshotAsync(JsonElement? args, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["filename"] = GetStringArg(args, "filename"),
            ["includeBase64"] = true
        };

        var (statusCode, responseText) = await PostJsonAsync("/screenshot", body.ToJsonString(), ct).ConfigureAwait(false);
        if (statusCode >= 400)
        {
            return CreateTextResult(responseText, isError: true);
        }

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
        string filePath = root.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? string.Empty : string.Empty;
        string? base64 = root.TryGetProperty("base64", out var b64) ? b64.GetString() : null;
        int width = root.TryGetProperty("width", out var w) && w.TryGetInt32(out int pw) ? pw : 0;
        int height = root.TryGetProperty("height", out var h) && h.TryGetInt32(out int ph) ? ph : 0;
        string? activeWindow = root.TryGetProperty("activeWindow", out var aw) ? aw.GetString() : null;
        string? theme = root.TryGetProperty("theme", out var th) ? th.GetString() : null;

        var metaSummary = new JsonObject
        {
            ["filePath"] = filePath,
            ["width"] = width,
            ["height"] = height,
            ["activeWindow"] = activeWindow,
            ["theme"] = theme
        };

        var contentArray = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = metaSummary.ToJsonString()
            }
        };

        if (!string.IsNullOrWhiteSpace(base64))
        {
            contentArray.Add(new JsonObject
            {
                ["type"] = "image",
                ["data"] = base64,
                ["mimeType"] = "image/png"
            });
        }

        return new JsonObject
        {
            ["content"] = contentArray,
            ["isError"] = false
        };
    }

    private async Task<JsonObject> GetDiagnosticsAsync(JsonElement? args, CancellationToken ct)
    {
        bool clear = GetBoolArg(args, "clear", defaultValue: false);
        string clearParam = clear ? "true" : "false";
        var (statusCode, responseText) = await GetRawAsync($"/diagnostics?clear={clearParam}", ct).ConfigureAwait(false);
        return CreateTextResult(responseText, isError: statusCode >= 400);
    }

    private async Task<string?> TryProbeHealthAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(1500));

        try
        {
            using var response = await _httpClient.GetAsync("/health", cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[McpToolHandler] Health probe yanıt vermedi: {ex.Message}").ConfigureAwait(false);
            return null;
        }
    }

    private async Task<(int StatusCode, string Body)> GetRawAsync(string relativeUrl, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(relativeUrl, ct).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ((int)response.StatusCode, body);
    }

    private async Task<(int StatusCode, string Body)> PostJsonAsync(string relativeUrl, string jsonBody, CancellationToken ct)
    {
        using var content = new StringContent(jsonBody, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        using var response = await _httpClient.PostAsync(relativeUrl, content, ct).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ((int)response.StatusCode, body);
    }

    private static string? ResolveExecutablePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        string[] relativeCandidates =
        [
            Path.Combine("KamatekCRM", "bin", "Debug", "net9.0-windows", "KamatekCrm.exe"),
            Path.Combine("KamatekCRM", "bin", "Debug", "net9.0-windows", "win-x64", "KamatekCrm.exe")
        ];

        string?[] startDirs = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];
        foreach (string? startDir in startDirs)
        {
            var dir = string.IsNullOrWhiteSpace(startDir) ? null : new DirectoryInfo(startDir);
            while (dir != null)
            {
                foreach (string rel in relativeCandidates)
                {
                    string candidate = Path.Combine(dir.FullName, rel);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static JsonObject CreateTextResult(string text, bool isError) => new()
    {
        ["content"] = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = text
            }
        },
        ["isError"] = isError
    };

    private static JsonNode? TryParseJsonNode(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[McpToolHandler] JSON ayrıştırma uyarısı: {ex.Message}");
            return null;
        }
    }

    private static string? GetStringArg(JsonElement? args, string propertyName)
    {
        if (args is { ValueKind: JsonValueKind.Object } obj &&
            obj.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static int GetIntArg(JsonElement? args, string propertyName, int defaultValue)
    {
        if (args is { ValueKind: JsonValueKind.Object } obj &&
            obj.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int val))
            {
                return val;
            }

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out int parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static bool GetBoolArg(JsonElement? args, string propertyName, bool defaultValue)
    {
        if (args is { ValueKind: JsonValueKind.Object } obj &&
            obj.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return prop.GetBoolean();
            }

            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out bool parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    public void Dispose() => _httpClient.Dispose();
}
