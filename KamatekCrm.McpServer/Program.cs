using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KamatekCrm.McpServer;

/// <summary>
/// KamatekCRM yerel MCP Stdio JSON-RPC 2.0 sunucusu.
/// KRİTİK KURAL: stdout yalnızca tek satırlık JSON-RPC 2.0 mesajları içindir;
/// tüm log ve hata çıktıları Console.Error (stderr) üzerinden yazılır.
/// </summary>
internal static class Program
{
    private const string DefaultProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false
    };

    public static async Task<int> Main()
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.InputEncoding = utf8NoBom;
        Console.OutputEncoding = utf8NoBom;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        using var toolHandler = new McpToolHandler();

        await Console.Error.WriteLineAsync("[KamatekCrm.McpServer] Stdio JSON-RPC 2.0 sunucusu başlatıldı.").ConfigureAwait(false);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                string? line = await Console.In.ReadLineAsync(cts.Token).ConfigureAwait(false);
                if (line == null)
                {
                    break;
                }

                line = line.TrimStart('\uFEFF');
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                JsonObject? response = await ProcessLineAsync(line, toolHandler, cts.Token).ConfigureAwait(false);
                if (response != null)
                {
                    string serialized = response.ToJsonString(CompactJsonOptions);
                    await Console.Out.WriteLineAsync(serialized).ConfigureAwait(false);
                    await Console.Out.FlushAsync(cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("[KamatekCrm.McpServer] İptal sinyali ile durduruldu.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[KamatekCrm.McpServer] Kritik döngü hatası: {ex}").ConfigureAwait(false);
            return 1;
        }

        return 0;
    }

    private static async Task<JsonObject?> ProcessLineAsync(string line, McpToolHandler toolHandler, CancellationToken ct)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            await Console.Error.WriteLineAsync($"[KamatekCrm.McpServer] Geçersiz JSON-RPC satırı: {ex.Message}").ConfigureAwait(false);
            return BuildErrorResponse(idNode: null, code: -32700, message: "Parse error");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return BuildErrorResponse(idNode: null, code: -32600, message: "Invalid Request");
            }

            bool hasId = root.TryGetProperty("id", out JsonElement idElement);
            JsonNode? idNode = hasId ? JsonNode.Parse(idElement.GetRawText()) : null;

            string? method = root.TryGetProperty("method", out JsonElement methodEl) && methodEl.ValueKind == JsonValueKind.String
                ? methodEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(method))
            {
                return hasId ? BuildErrorResponse(idNode, -32600, "Missing method") : null;
            }

            // Bildirimler (notifications/initialized vb.) id taşımaz ve yanıt döndürmez.
            if (!hasId || method.StartsWith("notifications/", StringComparison.Ordinal))
            {
                await Console.Error.WriteLineAsync($"[KamatekCrm.McpServer] Bildirim alındı: {method}").ConfigureAwait(false);
                return null;
            }

            JsonElement? parameters = root.TryGetProperty("params", out JsonElement paramsEl) ? paramsEl : null;

            return method switch
            {
                "initialize" => BuildSuccessResponse(idNode, BuildInitializeResult(parameters)),
                "ping" => BuildSuccessResponse(idNode, new JsonObject()),
                "tools/list" => BuildSuccessResponse(idNode, new JsonObject
                {
                    ["tools"] = McpToolDefinitions.CreateAll()
                }),
                "tools/call" => await HandleToolsCallAsync(idNode, parameters, toolHandler, ct).ConfigureAwait(false),
                _ => BuildErrorResponse(idNode, -32601, $"Method not found: {method}")
            };
        }
    }

    private static JsonObject BuildInitializeResult(JsonElement? parameters)
    {
        string protocolVersion = DefaultProtocolVersion;
        if (parameters is { ValueKind: JsonValueKind.Object } pObj &&
            pObj.TryGetProperty("protocolVersion", out var pv) &&
            pv.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(pv.GetString()))
        {
            protocolVersion = pv.GetString()!;
        }

        return new JsonObject
        {
            ["protocolVersion"] = protocolVersion,
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject
                {
                    ["listChanged"] = false
                }
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "kamatekcrm-mcp-server",
                ["version"] = "1.0.0"
            }
        };
    }

    private static async Task<JsonObject> HandleToolsCallAsync(
        JsonNode? idNode,
        JsonElement? parameters,
        McpToolHandler toolHandler,
        CancellationToken ct)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } pObj ||
            !pObj.TryGetProperty("name", out JsonElement nameEl) ||
            nameEl.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(nameEl.GetString()))
        {
            return BuildErrorResponse(idNode, -32602, "Invalid params: 'name' is required.");
        }

        string toolName = nameEl.GetString()!;
        JsonElement? toolArgs = pObj.TryGetProperty("arguments", out JsonElement argsEl) ? argsEl : null;

        JsonObject result = await toolHandler.CallToolAsync(toolName, toolArgs, ct).ConfigureAwait(false);
        return BuildSuccessResponse(idNode, result);
    }

    private static JsonObject BuildSuccessResponse(JsonNode? idNode, JsonObject result) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = idNode,
        ["result"] = result
    };

    private static JsonObject BuildErrorResponse(JsonNode? idNode, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = idNode,
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message
        }
    };
}
