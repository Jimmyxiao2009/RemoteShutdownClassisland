using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;   // Screen.PrimaryScreen — Windows only (net8.0-windows)

namespace RemoteShutdownPlugin.Services;

/// <summary>
/// 基于 <see cref="HttpListener"/> 的轻量 HTTP 服务器，提供截屏/关机/重启接口。
/// 此类完全独立于 UI 框架，可在任意线程使用。
/// </summary>
public sealed class WebServer
{
    // ── 字段 ──────────────────────────────────────────────────
    private readonly int _port;
    private string _username;
    private string _password;
    private readonly object _lock = new();

    private HttpListener? _listener;
    private Thread? _thread;

    // Bug 2 修复：_running 由监听线程读、Stop() 写，必须 volatile 保证跨线程可见性，
    // 避免 JIT/CPU 将旧值缓存在寄存器中导致线程无法终止。
    private volatile bool _running;

    // ── JPEG 编码器（只查找一次，复用）──────────────────────
    private static readonly ImageCodecInfo JpegCodec =
        ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

    // ── 构造 ──────────────────────────────────────────────────
    public WebServer(int port, string username, string password)
    {
        _port = port;
        _username = username;
        _password = password;
    }

    // ── 公开 API ──────────────────────────────────────────────
    public void Start()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_port}/");
        _listener.Start();
        _running = true;
        _thread = new Thread(Listen) { IsBackground = true };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
    }

    /// <summary>线程安全地更新凭据（热生效，无需重启服务器）。</summary>
    public void UpdateCredentials(string username, string password)
    {
        lock (_lock) { _username = username; _password = password; }
    }

    // ── 内部实现 ──────────────────────────────────────────────
    private void Listen()
    {
        while (_running)
        {
            try
            {
                var ctx = _listener!.GetContext();
                ThreadPool.QueueUserWorkItem(Handle, ctx);
            }
            catch { /* listener.Stop() 触发，忽略 */ }
        }
    }

    private void Handle(object? state)
    {
        var ctx = (HttpListenerContext)state!;
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            if (!Authenticate(req))
            {
                res.StatusCode = 401;
                res.AddHeader("WWW-Authenticate", "Basic realm=\"Remote Control\"");
                Respond(res, "Unauthorized", "text/plain; charset=utf-8");
                return;
            }

            string path = req.Url!.AbsolutePath;
            switch (path)
            {
                case "/":
                    Respond(res, BuildIndexPage(), "text/html; charset=utf-8");
                    break;
                case "/screenshot":
                    Respond(res, TakeScreenshot(), "application/json; charset=utf-8");
                    break;
                case "/shutdown" when req.HttpMethod == "POST":
                    Respond(res, RunShutdown(), "application/json; charset=utf-8");
                    break;
                case "/reboot" when req.HttpMethod == "POST":
                    Respond(res, RunReboot(), "application/json; charset=utf-8");
                    break;
                default:
                    res.StatusCode = 404;
                    Respond(res, "Not Found", "text/plain; charset=utf-8");
                    break;
            }
        }
        catch (Exception ex)
        {
            try { res.StatusCode = 500; Respond(res, Json(false, ex.Message), "application/json; charset=utf-8"); } catch { }
        }
        finally { try { res.Close(); } catch { } }
    }

    // ── 认证 ──────────────────────────────────────────────────
    private bool Authenticate(HttpListenerRequest req)
    {
        string? header = req.Headers["Authorization"];
        if (header is null || !header.StartsWith("Basic ")) return false;
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
        string[] parts = decoded.Split(':', 2);
        lock (_lock) return parts.Length == 2 && parts[0] == _username && parts[1] == _password;
    }

    // ── 页面 / API ────────────────────────────────────────────
    private string BuildIndexPage()
    {
        string html = GetEmbeddedHtml();
        return html.Replace("{{host}}", $"{GetLocalIp()}:{_port}");
    }

    private static string GetEmbeddedHtml()
    {
        var asm = Assembly.GetExecutingAssembly();
        string? name = Array.Find(asm.GetManifestResourceNames(),
            n => n.EndsWith("index.html", StringComparison.OrdinalIgnoreCase));
        if (name is null) return "<h1>index.html not found</h1>";
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string TakeScreenshot()
    {
        try
        {
            string? result = null;
            Exception? err = null;

            var t = new Thread(() =>
            {
                try
                {
                    // Bug 5 修复：PrimaryScreen 可能为 null（如 RDP Headless 环境），
                    // 去掉 ! 操作符，改为显式 null 检查并抛出有意义的异常。
                    var screen = System.Windows.Forms.Screen.PrimaryScreen
                        ?? throw new InvalidOperationException("无法获取主显示器信息（PrimaryScreen 为 null）。");

                    var bounds = screen.Bounds;
                    using var bmp = new Bitmap(bounds.Width, bounds.Height);
                    using (var g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);

                    // 等比缩放到最大宽度 1920
                    Bitmap dst = bmp;
                    const int MaxW = 1920;
                    if (bmp.Width > MaxW)
                    {
                        int h = (int)(bmp.Height * (MaxW / (float)bmp.Width));
                        dst = new Bitmap(bmp, MaxW, h);
                    }

                    // Bug 6 修复：通过 EncoderParameters 显式指定 JPEG 质量为 60，
                    // 避免默认质量导致响应体过大（原始 1920px 截图可能超过 3 MB）。
                    using var ms = new MemoryStream();
                    var encParams = new EncoderParameters(1);
                    encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 60L);
                    dst.Save(ms, JpegCodec, encParams);

                    if (!ReferenceEquals(dst, bmp)) dst.Dispose();
                    result = $"{{\"success\":true,\"image\":\"{Convert.ToBase64String(ms.ToArray())}\"}}";
                }
                catch (Exception ex) { err = ex; }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            return err is not null ? Json(false, err.Message) : result!;
        }
        catch (Exception ex) { return Json(false, ex.Message); }
    }

    private static string RunShutdown()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown", "/s /t 10")
                { CreateNoWindow = true, UseShellExecute = false });
            return "{\"success\":true,\"message\":\"电脑将在10秒后关机\"}";
        }
        catch (Exception ex) { return Json(false, ex.Message); }
    }

    private static string RunReboot()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown", "/r /t 10")
                { CreateNoWindow = true, UseShellExecute = false });
            return "{\"success\":true,\"message\":\"电脑将在10秒后重启\"}";
        }
        catch (Exception ex) { return Json(false, ex.Message); }
    }

    // ── 工具 ──────────────────────────────────────────────────
    private static void Respond(HttpListenerResponse res, string body, string ct)
    {
        byte[] data = Encoding.UTF8.GetBytes(body);
        res.ContentType = ct;
        res.ContentLength64 = data.Length;
        res.OutputStream.Write(data);
    }

    private static string Json(bool ok, string msg)
        => $"{{\"success\":{(ok ? "true" : "false")},\"error\":\"{Esc(msg)}\"}}";

    private static string Esc(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    internal static string GetLocalIp()
    {
        try
        {
            foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
}
