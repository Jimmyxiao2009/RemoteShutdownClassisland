using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteShutdownPlugin.Models;
using RemoteShutdownPlugin.Services;
using RemoteShutdownPlugin.Views.SettingsPages;
using System.IO;

namespace RemoteShutdownPlugin;

[PluginEntrance]
public class Plugin : PluginBase
{
    public static Plugin Instance { get; private set; } = null!;

    // 仅作公开只读访问点，实际 Settings 实例由 DI 容器统一持有（Bug 8 修复：
    // 通过 DI 注入的与此处引用的是同一个实例，不再存在双源访问歧义）。
    public PluginSettings Settings { get; private set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Instance = this;

        // ── 加载配置 ────────────────────────────────────────────
        string cfgPath = Path.Combine(PluginConfigFolder, "Settings.json");
        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(cfgPath);
        Settings.PropertyChanged += (_, _) =>
            ConfigureFileHelper.SaveConfig(cfgPath, Settings);

        // ── 注册服务（DI 注入的 PluginSettings 与 Plugin.Instance.Settings 是同一实例）──
        services.AddSingleton(Settings);
        services.AddSingleton<RemoteControlService>();
        services.AddHostedService(sp => sp.GetRequiredService<RemoteControlService>());

        // ── 注册设置页面 ─────────────────────────────────────────
        services.AddSettingsPage<RemoteControlSettingsPage>();
    }

}
