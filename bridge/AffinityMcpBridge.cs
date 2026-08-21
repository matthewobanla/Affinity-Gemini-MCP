using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AffinityMcpBridge {
    class Program {
        private static HttpClient client;
        private static StreamReader sseReader;
        private static string postUrl;
        private static int internalReqId = 100;
        private static readonly object sseLock = new object();

        static void Main(string[] args) {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            string line;
            while ((line = Console.ReadLine()) != null) {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try {
                    ProcessMessage(line.Trim());
                } catch (Exception ex) {
                    SendError(-1, -32603, "Internal bridge error: " + ex.Message);
                }
            }
        }

        private static void ProcessMessage(string json) {
            int id = ExtractId(json);
            string method = ExtractField(json, "method");

            if (method == "initialize") {
                EnsureAffinityConnected();
                string resp = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{" +
                    "\"protocolVersion\":\"2024-11-05\"," +
                    "\"capabilities\":{" +
                        "\"tools\":{}," +
                        "\"resources\":{}," +
                        "\"prompts\":{}" +
                    "}," +
                    "\"serverInfo\":{" +
                        "\"name\":\"AffinityBuiltinBridge\"," +
                        "\"version\":\"1.1.0\"" +
                    "}}}";
                Console.WriteLine(resp);
                Console.Out.Flush();
                return;
            }

            if (method == "notifications/initialized") {
                return;
            }

            if (method == "ping") {
                Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{}}");
                Console.Out.Flush();
                return;
            }

            if (method == "tools/list") {
                EnsureAffinityConnected();
                string toolsResult = CallAffinityRaw("tools/list", "{}");
                if (toolsResult.Contains("\"result\":")) {
                    Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":" + ExtractSubJson(toolsResult, "\"result\":") + "}");
                } else {
                    Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"tools\":[]}}");
                }
                Console.Out.Flush();
                return;
            }

            if (method == "tools/call") {
                EnsureAffinityConnected();
                string paramsJson = ExtractSubJson(json, "\"params\":");
                string toolName = ExtractField(paramsJson, "name");
                string argsJson = ExtractSubJson(paramsJson, "\"arguments\":");
                if (string.IsNullOrEmpty(argsJson)) argsJson = "{}";

                string callResult = CallAffinityTool(toolName, argsJson);
                if (callResult.Contains("\"result\":")) {
                    Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":" + ExtractSubJson(callResult, "\"result\":") + "}");
                } else if (callResult.Contains("\"error\":")) {
                    Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"error\":" + ExtractSubJson(callResult, "\"error\":") + "}");
                } else {
                    Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"content\":[{\"type\":\"text\",\"text\":" + EscapeString(callResult) + "}]}}");
                }
                Console.Out.Flush();
                return;
            }

            if (method == "resources/list") {
                string resourcesJson = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"resources\":[" +
                    "{" +
                        "\"uri\":\"affinity://document/info\"," +
                        "\"name\":\"Current Document Information\"," +
                        "\"description\":\"Returns active document properties: dimensions (width, height), units, color format, color space, session UUID, spread count, and artboard count.\"," +
                        "\"mimeType\":\"application/json\"" +
                    "}," +
                    "{" +
                        "\"uri\":\"affinity://spread/artboards\"," +
                        "\"name\":\"Spread Artboards List\"," +
                        "\"description\":\"Returns list of all artboards in the active spread with their names, indices, and bounding box coordinates.\"," +
                        "\"mimeType\":\"application/json\"" +
                    "}," +
                    "{" +
                        "\"uri\":\"affinity://selection\"," +
                        "\"name\":\"Active Layer Selection\"," +
                        "\"description\":\"Returns the currently selected layers in Affinity, including node IDs, types, names, and bounding boxes.\"," +
                        "\"mimeType\":\"application/json\"" +
                    "}" +
                "]}}";
                Console.WriteLine(resourcesJson);
                Console.Out.Flush();
                return;
            }

            if (method == "resources/read") {
                EnsureAffinityConnected();
                string paramsJson = ExtractSubJson(json, "\"params\":");
                string uri = ExtractField(paramsJson, "uri");
                HandleResourceRead(id, uri);
                return;
            }

            if (method == "prompts/list") {
                string promptsJson = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"prompts\":[" +
                    "{" +
                        "\"name\":\"create-isometric-artwork\"," +
                        "\"description\":\"Generate high-precision 2.5D isometric artwork, buildings, or icons with lighting and gradients in Affinity.\"," +
                        "\"arguments\":[" +
                            "{\"name\":\"theme\",\"description\":\"Theme or concept (e.g. 'cyberpunk city', 'fintech blockchain', 'datacenter')\",\"required\":true}," +
                            "{\"name\":\"palette\",\"description\":\"Color scheme or palette mood (e.g. 'neon dark mode', 'pastel', 'corporate blue')\",\"required\":false}," +
                            "{\"name\":\"artboardIndex\",\"description\":\"Target artboard index (default '0')\",\"required\":false}" +
                        "]" +
                    "}," +
                    "{" +
                        "\"name\":\"generate-icon-set\"," +
                        "\"description\":\"Generate a cohesive set of grid-aligned vector app icons on designated artboards.\"," +
                        "\"arguments\":[" +
                            "{\"name\":\"category\",\"description\":\"Icon category or theme (e.g. 'finance', 'weather', 'media player', 'settings')\",\"required\":true}," +
                            "{\"name\":\"count\",\"description\":\"Number of icons to generate (e.g. '6', '10')\",\"required\":false}," +
                            "{\"name\":\"gridSize\",\"description\":\"Pixel grid size for each icon (e.g. '128', '256')\",\"required\":false}," +
                            "{\"name\":\"style\",\"description\":\"Visual style (e.g. 'squircle glassmorphism', 'flat monochrome', 'gradient filled')\",\"required\":false}" +
                        "]" +
                    "}," +
                    "{" +
                        "\"name\":\"export-production-assets\"," +
                        "\"description\":\"Inspect and prepare artboards and slices for production asset export with visual verification.\"," +
                        "\"arguments\":[" +
                            "{\"name\":\"formats\",\"description\":\"Target export formats (e.g. 'SVG, PNG@2x, PDF')\",\"required\":false}," +
                            "{\"name\":\"artboardsOnly\",\"description\":\"Export individual artboards only ('true' or 'false')\",\"required\":false}" +
                        "]" +
                    "}" +
                "]}}";
                Console.WriteLine(promptsJson);
                Console.Out.Flush();
                return;
            }

            if (method == "prompts/get") {
                string paramsJson = ExtractSubJson(json, "\"params\":");
                string promptName = ExtractField(paramsJson, "name");
                string argsJson = ExtractSubJson(paramsJson, "\"arguments\":");
                HandlePromptGet(id, promptName, argsJson);
                return;
            }

            if (id > 0) {
                SendError(id, -32601, "Method not found: " + method);
            }
        }

        private static void HandleResourceRead(int id, string uri) {
            string script = "";
            if (uri == "affinity://document/info") {
                script = "(function() { try { const { Document } = require('/document'); const doc = Document.current; if (!doc) return JSON.stringify({ hasDocument: false, message: 'No active document open in Affinity.' }); const spread = doc.spreads.first; const abCount = (spread && spread.artboards) ? spread.artboards.length : 0; const bounds = spread ? { x: spread.bounds.x, y: spread.bounds.y, width: spread.bounds.width, height: spread.bounds.height } : null; return JSON.stringify({ hasDocument: true, name: doc.name || 'Untitled', sessionUuid: doc.sessionUuid || '', units: doc.units || 'Pixels', colourFormat: doc.colourFormat || 'RGB8', colourSpace: doc.colourSpace || 'sRGB', spreadCount: doc.spreads.length, artboardCount: abCount, bounds: bounds }); } catch (e) { return JSON.stringify({ error: e.message || String(e) }); } })()";
            } else if (uri == "affinity://spread/artboards") {
                script = "(function() { try { const { Document } = require('/document'); const doc = Document.current; if (!doc) return JSON.stringify({ hasDocument: false, artboards: [] }); const spread = doc.spreads.first; if (!spread) return JSON.stringify({ spreadIndex: -1, artboards: [] }); const abs = []; const count = spread.artboards ? spread.artboards.length : 0; for (let i = 0; i < count; i++) { const ab = spread.artboards[i]; abs.push({ index: i, name: ab.name || ('Artboard ' + (i + 1)), bounds: { x: ab.bounds.x, y: ab.bounds.y, width: ab.bounds.width, height: ab.bounds.height } }); } return JSON.stringify({ spreadIndex: 0, artboardCount: abs.length, artboards: abs }); } catch (e) { return JSON.stringify({ error: e.message || String(e) }); } })()";
            } else if (uri == "affinity://selection") {
                script = "(function() { try { const { Document } = require('/document'); const doc = Document.current; if (!doc) return JSON.stringify({ hasDocument: false, count: 0, selectedNodes: [] }); const sel = doc.selection || []; const nodes = []; for (let i = 0; i < sel.length; i++) { const n = sel[i]; nodes.push({ id: n.id || String(i), name: n.name || 'Node', type: n.type || 'Node', bounds: n.bounds ? { x: n.bounds.x, y: n.bounds.y, width: n.bounds.width, height: n.bounds.height } : null }); } return JSON.stringify({ count: nodes.length, selectedNodes: nodes }); } catch (e) { return JSON.stringify({ error: e.message || String(e) }); } })()";
            } else {
                SendError(id, -32602, "Unknown resource URI: " + uri);
                return;
            }

            try {
                string scriptArg = "{\"script\":" + EscapeString(script) + "}";
                string result = CallAffinityTool("execute_script", scriptArg);
                
                string contentText = "{}";
                if (result.Contains("\"text\":")) {
                    contentText = ExtractField(result, "text");
                    if (string.IsNullOrEmpty(contentText)) {
                        contentText = ExtractSubJson(result, "\"text\":");
                    }
                } else if (result.Contains("\"result\":")) {
                    contentText = ExtractSubJson(result, "\"result\":");
                } else {
                    contentText = result;
                }

                string resp = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"contents\":[{" +
                    "\"uri\":" + EscapeString(uri) + "," +
                    "\"mimeType\":\"application/json\"," +
                    "\"text\":" + EscapeString(contentText) +
                "}]}}";
                Console.WriteLine(resp);
                Console.Out.Flush();
            } catch (Exception ex) {
                SendError(id, -32603, "Error reading resource: " + ex.Message);
            }
        }

        private static void HandlePromptGet(int id, string promptName, string argsJson) {
            string theme = ExtractField(argsJson, "theme");
            string palette = ExtractField(argsJson, "palette");
            string artboardIndex = ExtractField(argsJson, "artboardIndex");
            string category = ExtractField(argsJson, "category");
            string count = ExtractField(argsJson, "count");
            string gridSize = ExtractField(argsJson, "gridSize");
            string style = ExtractField(argsJson, "style");
            string formats = ExtractField(argsJson, "formats");
            string artboardsOnly = ExtractField(argsJson, "artboardsOnly");

            if (string.IsNullOrEmpty(artboardIndex)) artboardIndex = "0";
            if (string.IsNullOrEmpty(count)) count = "6";
            if (string.IsNullOrEmpty(gridSize)) gridSize = "128";
            if (string.IsNullOrEmpty(formats)) formats = "SVG, PNG";

            string promptText = "";

            if (promptName == "create-isometric-artwork") {
                if (string.IsNullOrEmpty(theme)) theme = "Modern Isometric Cityscape";
                if (string.IsNullOrEmpty(palette)) palette = "Vibrant gradient with deep contrast";
                promptText = "You are an expert Affinity Designer vector automation engineer. Create a high-detail 2.5D isometric vector artwork with the following specifications:\\n\\n" +
                    "- Theme: " + theme + "\\n" +
                    "- Color Palette: " + palette + "\\n" +
                    "- Target Artboard: " + artboardIndex + "\\n\\n" +
                    "Rules to follow:\\n" +
                    "1. Query 'affinity://spread/artboards' to find artboard bounds.\\n" +
                    "2. Construct 30-degree isometric projection planes (Top: 30°/-30°, Left: -30° vertical, Right: 30° vertical) using CurveBuilder and PolyCurve.\\n" +
                    "3. Apply directional lighting with linear/radial gradients using matrix transforms (Transform.data).\\n" +
                    "4. Batch all nodes into AddChildNodesCommandBuilder for a single atomic undo transaction.\\n" +
                    "5. After creating the artwork, call 'render_spread' to visually inspect the final composition.";
            } else if (promptName == "generate-icon-set") {
                if (string.IsNullOrEmpty(category)) category = "General UI";
                if (string.IsNullOrEmpty(style)) style = "Modern squircle glassmorphism with subtle gradient fills";
                promptText = "You are an expert icon designer and Affinity vector automation specialist. Generate a cohesive set of " + count + " vector icons for the category '" + category + "'.\\n\\n" +
                    "- Icon Style: " + style + "\\n" +
                    "- Grid Size: " + gridSize + "x" + gridSize + " px per icon\\n\\n" +
                    "Execution Workflow:\\n" +
                    "1. Proactively read 'affinity://spread/artboards' to inspect existing layout.\\n" +
                    "2. Compute grid positions with consistent padding and optical centering.\\n" +
                    "3. Generate icon backplates (e.g. ShapeRectangle with corner radius) and foreground vector glyphs (CurveBuilder/PolyCurve or native shapes).\\n" +
                    "4. Execute via 'execute_script' with AddChildNodesCommandBuilder for atomic transaction.\\n" +
                    "5. Perform visual verification using 'render_spread'.";
            } else if (promptName == "export-production-assets") {
                promptText = "You are an Affinity production pipeline assistant. Prepare and inspect the current document for asset export:\\n\\n" +
                    "- Formats: " + formats + "\\n" +
                    "- Artboards Only: " + (string.IsNullOrEmpty(artboardsOnly) ? "true" : artboardsOnly) + "\\n\\n" +
                    "Steps:\\n" +
                    "1. Read 'affinity://document/info' and 'affinity://spread/artboards' to audit document resolution and geometry.\\n" +
                    "2. Validate all artboard names and bounding box alignments (prevent subpixel blur).\\n" +
                    "3. Render visual preview with 'render_spread' for QA verification.";
            } else {
                SendError(id, -32602, "Unknown prompt: " + promptName);
                return;
            }

            string resp = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{" +
                "\"description\":" + EscapeString("Recipe for " + promptName) + "," +
                "\"messages\":[{" +
                    "\"role\":\"user\"," +
                    "\"content\":{" +
                        "\"type\":\"text\"," +
                        "\"text\":" + EscapeString(promptText) +
                    "}" +
                "}]" +
            "}}";
            Console.WriteLine(resp);
            Console.Out.Flush();
        }

        private static void EnsureAffinityConnected() {
            if (client != null && postUrl != null) return;

            lock (sseLock) {
                if (client != null && postUrl != null) return;

                client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);

                string[] endpoints = new string[] { "http://[::1]:6767/sse", "http://localhost:6767/sse", "http://127.0.0.1:6767/sse" };
                Stream sseStream = null;

                foreach (string ep in endpoints) {
                    try {
                        sseStream = client.GetStreamAsync(ep).Result;
                        break;
                    } catch {}
                }

                if (sseStream == null) {
                    throw new Exception("Unable to connect to Affinity. Please ensure Affinity is running with MCP enabled under Edit > Settings > Model Context Protocol.");
                }

                sseReader = new StreamReader(sseStream, Encoding.UTF8);

                string endpointPath = null;
                while (true) {
                    string l = sseReader.ReadLine();
                    if (l == null) break;
                    if (l.StartsWith("data:")) {
                        endpointPath = l.Substring(5).Trim();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(endpointPath)) {
                    throw new Exception("Failed to receive endpoint from Affinity SSE stream.");
                }

                postUrl = "http://[::1]:6767" + endpointPath;

                // 1. Initialize Affinity with protocol 2025-11-25
                string initJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"AntigravityIdeBridge\",\"version\":\"1.1.0\"}}}";
                client.PostAsync(postUrl, new StringContent(initJson, Encoding.UTF8, "application/json")).Wait();

                while (true) {
                    string l = sseReader.ReadLine();
                    if (l == null || (l.StartsWith("data:") && l.Contains("\"id\":1"))) break;
                }

                // 2. notifications/initialized
                string notifJson = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";
                client.PostAsync(notifJson, new StringContent(notifJson, Encoding.UTF8, "application/json")).Wait();

                // 3. Mandatory Preamble
                CallAffinityToolInternal("read_sdk_documentation_topic", "{\"filename\":\"preamble\"}");
            }
        }

        private static string CallAffinityTool(string toolName, string argsJson) {
            return CallAffinityToolInternal(toolName, argsJson);
        }

        private static string CallAffinityRaw(string method, string paramsJson) {
            lock (sseLock) {
                int reqId = Interlocked.Increment(ref internalReqId);
                string payload = "{\"jsonrpc\":\"2.0\",\"id\":" + reqId + ",\"method\":\"" + method + "\",\"params\":" + paramsJson + "}";
                client.PostAsync(postUrl, new StringContent(payload, Encoding.UTF8, "application/json")).Wait();

                while (true) {
                    string l = sseReader.ReadLine();
                    if (l == null) break;
                    if (l.StartsWith("data:") && l.Contains("\"id\":" + reqId)) {
                        return l.Substring(5).Trim();
                    }
                }
                return "{}";
            }
        }

        private static string CallAffinityToolInternal(string toolName, string argsJson) {
            lock (sseLock) {
                int reqId = Interlocked.Increment(ref internalReqId);
                string payload = "{\"jsonrpc\":\"2.0\",\"id\":" + reqId + ",\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + argsJson + "}}";
                client.PostAsync(postUrl, new StringContent(payload, Encoding.UTF8, "application/json")).Wait();

                while (true) {
                    string l = sseReader.ReadLine();
                    if (l == null) break;
                    if (l.StartsWith("data:") && l.Contains("\"id\":" + reqId)) {
                        return l.Substring(5).Trim();
                    }
                }
                return "{}";
            }
        }

        private static void SendError(int id, int code, string message) {
            string err = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"error\":{\"code\":" + code + ",\"message\":" + EscapeString(message) + "}}";
            Console.WriteLine(err);
            Console.Out.Flush();
        }

        private static int ExtractId(string json) {
            int idx = json.IndexOf("\"id\":");
            if (idx == -1) return 0;
            idx += 5;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':')) idx++;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            int val;
            if (int.TryParse(json.Substring(idx, end - idx), out val)) return val;
            return 0;
        }

        private static string ExtractField(string json, string field) {
            string search = "\"" + field + "\":";
            int idx = json.IndexOf(search);
            if (idx == -1) return "";
            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '"')) idx++;
            int end = json.IndexOf("\"", idx);
            if (end == -1) return "";
            return json.Substring(idx, end - idx);
        }

        private static string ExtractSubJson(string json, string key) {
            int idx = json.IndexOf(key);
            if (idx == -1) return "{}";
            idx += key.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            if (idx >= json.Length) return "{}";

            char open = json[idx];
            if (open != '{' && open != '[') {
                int end = json.IndexOfAny(new char[] { ',', '}' }, idx);
                if (end == -1) end = json.Length;
                return json.Substring(idx, end - idx).Trim();
            }

            char close = open == '{' ? '}' : ']';
            int depth = 0;
            for (int i = idx; i < json.Length; i++) {
                if (json[i] == open) depth++;
                else if (json[i] == close) {
                    depth--;
                    if (depth == 0) {
                        return json.Substring(idx, i - idx + 1);
                    }
                }
            }
            return "{}";
        }

        private static string EscapeString(string s) {
            if (s == null) return "\"\"";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s) {
                switch (c) {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }
    }
}
