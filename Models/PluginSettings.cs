using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RemoteShutdownPlugin.Models;

/// <summary>
/// 插件配置，通过 ConfigureFileHelper 自动序列化到 JSON。
/// </summary>
public class PluginSettings : INotifyPropertyChanged
{
    private int _port = 10543;
    private string _username = "admin";
    private string _password = "password123";
    private bool _startOnLoad = true;

    /// <summary>HTTP 监听端口，默认 10543。</summary>
    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    /// <summary>Basic Auth 用户名（固定 admin）。</summary>
    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }

    /// <summary>Basic Auth 密码（至少 6 位）。</summary>
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); }
    }

    /// <summary>插件加载后是否自动启动 HTTP 服务器。</summary>
    public bool StartOnLoad
    {
        get => _startOnLoad;
        set { _startOnLoad = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
