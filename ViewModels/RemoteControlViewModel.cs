using RemoteShutdownPlugin.Models;
using RemoteShutdownPlugin.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace RemoteShutdownPlugin.ViewModels;

/// <summary>
/// 设置页面 ViewModel，与 Avalonia 控件完全解耦。
/// </summary>
public sealed class RemoteControlViewModel : INotifyPropertyChanged
{
    private readonly RemoteControlService _service;
    private string _pendingPassword;

    // ── 构造 ──────────────────────────────────────────────────
    public RemoteControlViewModel(RemoteControlService service)
    {
        _service = service;

        // Bug 8 修复：Settings 统一通过 Plugin.Instance.Settings 访问。
        // DI 容器注册的是同一个实例（AddSingleton(Settings)），
        // 因此这里访问的与 RemoteControlService 注入的是完全相同的对象，无双源问题。
        _pendingPassword = Plugin.Instance.Settings.Password;

        StartCommand = new RelayCommand(
            _ => { _service.StartServer(); Refresh(); },
            _ => !_service.IsRunning);

        StopCommand = new RelayCommand(
            _ => { _service.StopServer(); Refresh(); },
            _ => _service.IsRunning);

        SavePasswordCommand = new RelayCommand(_ =>
        {
            if (_pendingPassword.Length < 6) return;
            Settings.Password = _pendingPassword;
            _service.UpdateCredentials();
        });
    }

    // ── 属性 ──────────────────────────────────────────────────
    // 统一读取 Plugin.Instance.Settings（与 DI 注入的同一实例）
    public PluginSettings Settings => Plugin.Instance.Settings;

    public bool IsRunning => _service.IsRunning;

    public string StatusMessage => IsRunning ? "● 服务运行中" : "● 服务已停止";

    public string AccessUrl => IsRunning
        ? $"访问地址：{_service.GetAccessUrl()}"
        : "（服务未启动）";

    public string RawAccessUrl => _service.GetAccessUrl();

    public string PendingPassword
    {
        get => _pendingPassword;
        set { _pendingPassword = value; OnPropertyChanged(); }
    }

    // ── 命令 ──────────────────────────────────────────────────
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SavePasswordCommand { get; }

    // ── INPC ──────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Refresh()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(AccessUrl));
        OnPropertyChanged(nameof(RawAccessUrl));
        ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
    }
}

// ── 简易 RelayCommand ─────────────────────────────────────────
internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => _execute(p);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
