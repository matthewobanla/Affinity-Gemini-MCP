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
            // Minimal fast JSON parsing
            int id = ExtractId(json);
            string method = ExtractField(json, "method");

            if (method == "initialize") {
                EnsureAffinityConnected();
                // Respond to IDE
                string resp = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"AffinityBuiltinBridge\",\"version\":\"1.0.0\"}}}";
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

            if (id > 0) {
                SendError(id, -32601, "Method not found: " + method);
            }
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
                string initJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"AntigravityIdeBridge\",\"version\":\"1.0.0\"}}}";
                client.PostAsync(postUrl, new StringContent(initJson, Encoding.UTF8, "application/json")).Wait();

                while (true) {
                    string l = sseReader.ReadLine();
                    if (l == null || (l.StartsWith("data:") && l.Contains("\"id\":1"))) break;
                }

                // 2. notifications/initialized
                string notifJson = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";
                client.PostAsync(postUrl, new StringContent(notifJson, Encoding.UTF8, "application/json")).Wait();

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
