using Avalonia.Input;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using RemoteShutdownPlugin.Services;
using RemoteShutdownPlugin.ViewModels;
using System.Diagnostics;

namespace RemoteShutdownPlugin.Views.SettingsPages;

[SettingsPageInfo("rspci.jimmyxiao.settings", "远程关机/截屏控制")]
public partial class RemoteControlSettingsPage : SettingsPageBase
{
    // 可空：无参构造时 _vm 为 null（仅设计器/XAML运行时加载器使用）
    private readonly RemoteControlViewModel? _vm;

    /// <summary>
    /// 供 Avalonia XAML 运行时加载器 / 设计器使用的无参构造函数。
    /// 修复 AVLN3001：运行时加载器要求至少存在一个公共无参构造函数。
    /// 实际运行时由下方的 DI 构造函数实例化。
    /// </summary>
    public RemoteControlSettingsPage()
    {
        InitializeComponent();
    }

    /// <summary>DI 注入构造函数，ClassIsland 宿主通过 AddSettingsPage 解析时使用。</summary>
    public RemoteControlSettingsPage(RemoteControlService service)
    {
        _vm = new RemoteControlViewModel(service);
        DataContext = _vm;
        InitializeComponent();
    }

    // 点击访问地址链接，用浏览器打开
    private void AccessUrl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null || !_vm.IsRunning) return;
        Process.Start(new ProcessStartInfo(_vm.RawAccessUrl) { UseShellExecute = true });
    }
}
