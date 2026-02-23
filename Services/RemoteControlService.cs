using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RemoteShutdownPlugin.Models;
using System.Diagnostics;

namespace RemoteShutdownPlugin.Services;

/// <summary>
/// 随 ClassIsland 宿主生命周期启停的远程控制服务。
/// </summary>
public sealed class RemoteControlService : IHostedService
{
    private readonly PluginSettings _settings;
    private readonly ILogger<RemoteControlService> _logger;
    private WebServer? _server;

    // Bug 3 修复：IsRunning 由多个线程（宿主线程、UI线程）并发读写，
    // 必须加 volatile 保证可见性。
    private volatile bool _isRunning;
    public bool IsRunning => _isRunning;

    public RemoteControlService(PluginSettings settings, ILogger<RemoteControlService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    // ── IHostedService ────────────────────────────────────────
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_settings.StartOnLoad) StartServer();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopServer();
        return Task.CompletedTask;
    }

    // ── 公开控制 API（供设置页调用）──────────────────────────
    public void StartServer()
    {
        if (_isRunning) return;
        try
        {
            EnsureUrlAcl(_settings.Port);
            _server = new WebServer(_settings.Port, _settings.Username, _settings.Password);
            _server.Start();
            _isRunning = true;
            _logger.LogInformation("RemoteControl 服务器已启动，端口 {Port}", _settings.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemoteControl 服务器启动失败");
        }
    }

    public void StopServer()
    {
        if (!_isRunning) return;
        _server?.Stop();
        _server = null;
        _isRunning = false;
        _logger.LogInformation("RemoteControl 服务器已停止");
    }

    /// <summary>热更新凭据，无需重启服务器。</summary>
    public void UpdateCredentials()
        => _server?.UpdateCredentials(_settings.Username, _settings.Password);

    public string GetAccessUrl()
        => $"http://{WebServer.GetLocalIp()}:{_settings.Port}";

    // ── URL ACL（首次需要一次 UAC 授权）─────────────────────
    private static void EnsureUrlAcl(int port)
    {
        if (IsAclRegistered(port)) return;
        try
        {
            var p = Process.Start(new ProcessStartInfo("netsh",
                $"http add urlacl url=http://+:{port}/ user=Everyone")
            { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
            p?.WaitForExit();
        }
        catch { /* 用户取消 UAC */ }
    }

    private static bool IsAclRegistered(int port)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("netsh",
                $"http show urlacl url=http://+:{port}/")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            string output = p!.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output.Contains(port.ToString());
        }
        catch { return false; }
    }
}
