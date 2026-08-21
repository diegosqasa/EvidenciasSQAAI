using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EvidenciasSQA.Core.Helpers;

namespace EvidenciasSQA.HttpListeners;

/// <summary>
/// Listener HTTP loopback que recibe capturas de la extensión web Ext_Web
/// y las integra con el flujo del Visor.
///
/// Contrato 100% compatible con ext-web-visor-greenshot.md:
/// - POST /api/capture-binary   → body raw PNG + headers X-SQA-*  → {success,status:'processing',autoCopyOnCapture}
/// - POST /api/capture          → JSON {dataUrl,url,title,timestamp,browser,os} → ídem
/// - POST /api/capture-batch    → JSON {captures:[{dataUrl,...}]} → {success,total,results:[{success}]}
/// - GET  /api/peek-sequence    → {success,sequence,label} (NO incrementa el contador)
/// - GET  /api/show             → trigger UI-only de apertura del visor → {success}
/// - GET  /api/health           → {success}
///
/// Responsabilidades (aditivas): levantar el listener, validar/extraer el payload,
/// disparar <see cref="CaptureReceived"/> / <see cref="ViewerOpenRequested"/> y
/// responder el JSON del contrato. El suscriptor (App.xaml.cs) es quien persiste
/// la captura y notifica al Visor (SqaEvents.CaptureSaved). El listener nunca
/// toca SqaCaptureFlow, UI ni procesamiento de imágenes.
/// </summary>
public sealed class SqaHttpListener : IDisposable
{
    public const string DefaultPort = "3000";

    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly Task _listenerTask;

    /// <summary>
    /// Evento disparado por cada captura recibida (una por elemento en /capture-batch).
    /// El suscriptor decide cómo persistir (App.xaml.cs: guardar en CapturasQA +
    /// BakeCorporateHeader si hace falta + SqaEvents.RaiseCaptureSaved).
    /// </summary>
    public event EventHandler<CaptureRequestEventArgs>? CaptureReceived;

    /// <summary>
    /// Evento disparado cuando la extensión solicita abrir/foreground el visor
    /// (GET /api/show). Trigger UI-only: solo visibilidad, sin negocio.
    /// </summary>
    public event EventHandler? ViewerOpenRequested;

    /// <summary>
    /// Contexto del payload de captura recibido.
    /// </summary>
    public sealed class CaptureRequestEventArgs : EventArgs
    {
        /// <summary>URL de la pestaña origen de la captura (X-SQA-Url o JSON url).</summary>
        public string? Url { get; set; }

        /// <summary>Título de la página/ventana capturada.</summary>
        public string? Title { get; set; }

        /// <summary>Timestamp ISO 8601 de la captura (X-SQA-Timestamp o JSON timestamp).</summary>
        public string? Timestamp { get; set; }

        /// <summary>Nombre del navegador (ej. "Chrome v124.0").</summary>
        public string? Browser { get; set; }

        /// <summary>Sistema operativo (ej. "Windows 10").</summary>
        public string? Os { get; set; }

        /// <summary>True si la captura ya trae el header corporativo horneado.</summary>
        public bool HasHeader { get; set; }

        /// <summary>Datos base64 de la imagen (JSON dataUrl, sin prefijo).</summary>
        public string? ImageBase64 { get; set; }

        /// <summary>Bytes de la imagen (POST /api/capture-binary con body raw).</summary>
        public byte[]? ImageBytes { get; set; }

        /// <summary>
        /// Decodifica los bytes de la imagen desde cualquiera de las dos vías:
        /// binario directo (ImageBytes) o dataUrl base64 (ImageBase64, con/sin prefijo data:*;base64,).
        /// </summary>
        public byte[]? ResolveImageBytes()
        {
            if (ImageBytes is { Length: > 0 })
            {
                return ImageBytes;
            }

            if (string.IsNullOrEmpty(ImageBase64))
            {
                return null;
            }

            string b64 = ImageBase64;
            int commaIdx = b64.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (commaIdx >= 0)
            {
                b64 = b64.Substring(commaIdx + "base64,".Length);
            }

            try
            {
                return Convert.FromBase64String(b64);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Constructor. Puerto por defecto 3000 (el que usa la extensión), configurable
    /// vía env var SQA_HTTP_LISTENER_PORT. Escucha solo en loopback (127.0.0.1):
    /// no requiere urlacl ni permisos de administrador.
    /// </summary>
    public SqaHttpListener(string? port = null)
    {
        string effectivePort = port
            ?? Environment.GetEnvironmentVariable("SQA_HTTP_LISTENER_PORT")
            ?? DefaultPort;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{effectivePort}/");
        _listener.Start();

        _listenerTask = Task.Run(ListenLoop, _cts.Token);
    }

    /// <summary>
    /// Bucle principal de escucha. Procesa cada solicitud en un hilo de fondo.
    /// </summary>
    private async Task ListenLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context));
            }
            catch (HttpListenerException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(100, _cts.Token);
            }
        }
    }

    /// <summary>
    /// Enruta una solicitud entrante según método y path del contrato.
    /// Las respuestas son INMEDIATAS (la persistencia la hace el suscriptor
    /// en Task.Run, sin bloquear el request de la extensión).
    /// </summary>
    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            string path = request.Url?.AbsolutePath ?? string.Empty;
            bool isGet = request.HttpMethod == "GET";

            if (isGet)
            {
                if (path.EndsWith("/peek-sequence"))
                {
                    WriteJson(context, 200, BuildPeekSequenceResponse());
                    return;
                }

                if (path.EndsWith("/show"))
                {
                    ViewerOpenRequested?.Invoke(this, EventArgs.Empty);
                    WriteJson(context, 200, "{\"success\":true}");
                    return;
                }

                if (path.EndsWith("/health"))
                {
                    WriteJson(context, 200, "{\"success\":true}");
                    return;
                }

                WriteJson(context, 404, "{\"success\":false,\"error\":\"not_found\"}");
                return;
            }

            if (request.HttpMethod == "POST")
            {
                if (path.EndsWith("/capture-binary"))
                {
                    HandleCaptureBinary(context);
                    return;
                }

                if (path.EndsWith("/capture"))
                {
                    HandleCaptureJson(context);
                    return;
                }

                if (path.EndsWith("/capture-batch"))
                {
                    HandleCaptureBatch(context);
                    return;
                }

                WriteJson(context, 404, "{\"success\":false,\"error\":\"not_found\"}");
                return;
            }

            WriteJson(context, 405, "{\"success\":false,\"error\":\"method_not_allowed\"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SqaHttpListener] Error procesando request: {ex.Message}");
            WriteJson(context, 500, "{\"success\":false,\"error\":\"internal_error\"}");
        }
    }

    /// <summary>
    /// POST /api/capture-binary: body raw PNG + metadatos en headers X-SQA-*.
    /// </summary>
    private void HandleCaptureBinary(HttpListenerContext context)
    {
        var request = context.Request;
        NameValueCollection h = request.Headers;

        byte[] bytes;
        try
        {
            using var ms = new MemoryStream();
            request.InputStream.CopyTo(ms);
            bytes = ms.ToArray();
        }
        catch
        {
            WriteJson(context, 400, "{\"success\":false,\"error\":\"invalid_body\"}");
            return;
        }

        if (bytes.Length == 0)
        {
            WriteJson(context, 400, "{\"success\":false,\"error\":\"empty_body\"}");
            return;
        }

        var args = new CaptureRequestEventArgs
        {
            Url = DecodeHeader(h["X-SQA-Url"]),
            Title = DecodeHeader(h["X-SQA-Title"]),
            Timestamp = h["X-SQA-Timestamp"],
            Browser = h["X-SQA-Browser"],
            Os = h["X-SQA-OS"],
            HasHeader = IsTruthy(h["X-SQA-Has-Header"]),
            ImageBytes = bytes
        };

        CaptureReceived?.Invoke(this, args);
        WriteJson(context, 200, BuildProcessingResponse());
    }

    /// <summary>
    /// POST /api/capture: JSON {dataUrl,url,title,timestamp,browser,os,hasHeader}.
    /// </summary>
    private void HandleCaptureJson(HttpListenerContext context)
    {
        if (!TryReadJson(context.Request, out JsonDocument? doc) || doc == null)
        {
            WriteJson(context, 400, "{\"success\":false,\"error\":\"invalid_json\"}");
            return;
        }

        using (doc)
        {
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("dataUrl", out JsonElement dataUrl) || dataUrl.ValueKind != JsonValueKind.String)
            {
                WriteJson(context, 400, "{\"success\":false,\"error\":\"dataUrl_required\"}");
                return;
            }

            var args = new CaptureRequestEventArgs
            {
                Url = GetString(root, "url"),
                Title = GetString(root, "title"),
                Timestamp = GetString(root, "timestamp"),
                Browser = GetString(root, "browser"),
                Os = GetString(root, "os"),
                HasHeader = GetBool(root, "hasHeader"),
                ImageBase64 = dataUrl.GetString()
            };

            CaptureReceived?.Invoke(this, args);
            WriteJson(context, 200, BuildProcessingResponse());
        }
    }

    /// <summary>
    /// POST /api/capture-batch: JSON {captures:[{dataUrl,url,title,timestamp}]}.
    /// Dispara un CaptureReceived por captura válida y reporta results por elemento
    /// (misma semántica del content script: cada índice del array responde su estado).
    /// </summary>
    private void HandleCaptureBatch(HttpListenerContext context)
    {
        if (!TryReadJson(context.Request, out JsonDocument? doc) || doc == null)
        {
            WriteJson(context, 400, "{\"success\":false,\"error\":\"invalid_json\"}");
            return;
        }

        using (doc)
        {
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("captures", out JsonElement captures) || captures.ValueKind != JsonValueKind.Array)
            {
                WriteJson(context, 400, "{\"success\":false,\"error\":\"captures_required\"}");
                return;
            }

            int total = 0;
            var results = new StringBuilder("[");
            foreach (JsonElement item in captures.EnumerateArray())
            {
                bool ok = false;
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("dataUrl", out JsonElement dataUrl) && dataUrl.ValueKind == JsonValueKind.String)
                {
                    var args = new CaptureRequestEventArgs
                    {
                        Url = GetString(item, "url"),
                        Title = GetString(item, "title"),
                        Timestamp = GetString(item, "timestamp"),
                        Browser = GetString(item, "browser"),
                        Os = GetString(item, "os"),
                        HasHeader = GetBool(item, "hasHeader"),
                        ImageBase64 = dataUrl.GetString()
                    };

                    CaptureReceived?.Invoke(this, args);
                    ok = true;
                }

                if (results.Length > 1)
                {
                    results.Append(',');
                }
                results.Append(ok ? "{\"success\":true}" : "{\"success\":false}");
                total++;
            }
            results.Append(']');

            WriteJson(context, 200, $"{{\"success\":true,\"status\":\"processing\",\"total\":{total},\"results\":{results}}}");
        }
    }

    /// <summary>
    /// GET /api/peek-sequence: próximo id de evidencia SIN incrementarlo
    /// (el content script lo usa para el header del cliente).
    /// </summary>
    private static string BuildPeekSequenceResponse()
    {
        string folder = GetOutputFolder();
        int next = SqaEvidenceSequence.Peek(folder);
        return $"{{\"success\":true,\"sequence\":{next},\"label\":\"Evidencias_{next:D2}\"}}";
    }

    /// <summary>
    /// Respuesta estándar de aceptación en cola (el guardado real es asíncrono).
    /// </summary>
    private static string BuildProcessingResponse()
    {
        return "{\"success\":true,\"status\":\"processing\",\"autoCopyOnCapture\":false}";
    }

    /// <summary>
    /// Carpeta de salida: ~/CapturasQA (la misma del flujo de captura del Tray).
    /// </summary>
    private static string GetOutputFolder()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CapturasQA");
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch
        {
            // Best-effort: si no se puede crear, el caller falla de forma controlada.
        }
        return folder;
    }

    private static bool TryReadJson(HttpListenerRequest request, out JsonDocument? doc)
    {
        doc = null;
        try
        {
            using var stream = request.InputStream;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            doc = JsonDocument.Parse(reader.ReadToEnd());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetString(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }

    private static bool GetBool(JsonElement obj, string property)
    {
        return obj.TryGetProperty(property, out JsonElement el) && el.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Decodifica un header URL-encoded (la extensión envía X-SQA-Url codificado
    /// para transportar la URL completa en una sola línea de header).
    /// </summary>
    private static string? DecodeHeader(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static bool IsTruthy(string? value)
    {
        return value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");
    }

    private static void WriteJson(HttpListenerContext context, int statusCode, string json)
    {
        try
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = body.Length;
            context.Response.OutputStream.Write(body, 0, body.Length);
            context.Response.Close();
        }
        catch
        {
            try { context.Response.Close(); } catch { }
        }
    }

    /// <summary>
    /// Detiene el listener de forma ordenada.
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        _listener?.Close();
    }
}