# 远程关机/截屏控制插件

<p align="center">
  <img src="icon.ico" width="96" alt="Plugin Icon"/>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/插件%20ID-rspci.jimmyxiao-0078D4?style=flat-square"/>
  <img src="https://img.shields.io/badge/ClassIsland-2.x-0078D4?style=flat-square&logo=windows&logoColor=white"/>
  <img src="https://img.shields.io/badge/.NET-8.0--windows-512BD4?style=flat-square&logo=dotnet&logoColor=white"/>
  <img src="https://img.shields.io/badge/平台-Windows only-0078D4?style=flat-square"/>
  <img src="https://img.shields.io/badge/版本-2.0.0.0-green?style=flat-square"/>
</p>

通过局域网网页端，远程对运行 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 的 Windows 电脑进行**截屏预览、关机、重启**操作。无需安装任何客户端，打开手机/平板浏览器即可使用。

---

## 功能

| 功能 | 说明 |
|------|------|
| 📸 实时截屏 | 截取主屏幕并以 JPEG 格式实时传回浏览器 |
| 🔌 远程关机 | 发送关机命令，10 秒后执行 |
| 🔄 远程重启 | 发送重启命令，10 秒后执行 |
| 🔐 Basic Auth | 用户名 + 密码保护，防止局域网内未授权访问 |
| 🔁 热更新凭据 | 在设置页修改密码后立即生效，无需重启服务器 |
| ⚡ 自动启动 | 可选：ClassIsland 加载时自动启动 HTTP 服务器 |

---

## 使用方法

### 1. 安装插件

在 ClassIsland 插件市场搜索 **`rspci.jimmyxiao`** 一键安装，或将 `.cipx` 文件拖入 ClassIsland 插件管理页面手动安装。

### 2. 配置

进入 **ClassIsland 设置 → 远程关机/截屏控制**：

- **监听端口**：默认 `10543`，可修改为任意 1024–65535 端口
- **自动启动**：勾选后 ClassIsland 每次启动时自动开启服务
- **密码**：默认 `password123`，建议修改为至少 6 位的强密码

点击 **启动** 按钮，页面上将显示局域网访问地址（如 `http://192.168.1.100:10543`）。

### 3. 访问控制面板

在同一局域网内，用任意浏览器打开访问地址，输入：

- 用户名：`admin`
- 密码：设置页中配置的密码

即可进入网页控制面板。

> **提示**：首次启动时会弹出 UAC 提权窗口，用于向 Windows 注册 HTTP URL 保留（`netsh http add urlacl`）。允许后后续启动无需再次授权。

---

## 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11（仅限 Windows） |
| ClassIsland | 2.x（apiVersion ≥ 2.0.0.1） |
| .NET 运行时 | 8.0（随 ClassIsland 附带，无需单独安装） |
| 网络 | 局域网（控制端与被控端在同一网络下） |

---

## 安全说明

- 本插件使用 **HTTP Basic Auth**，凭据以 Base64 编码传输，**未加密**。
- 请仅在可信的局域网（家庭/教室内网）中使用，**不要将端口暴露到公网**。
- 建议将密码修改为强密码，并在不使用时通过设置页停止服务器。

---

## 构建

```powershell
git clone https://github.com/jimmyxiao/RemoteShutdownPlugin.git
cd RemoteShutdownPlugin

# 构建并打包为 plugin.cipx
dotnet publish -p:CreateCipx=true --no-self-contained
```

输出文件位于 `bin\Release\net8.0-windows\publish\` 目录下，Release Tag 须严格遵循 `a.b.c.d` 格式（如 `2.0.0.0`）。

---

## 上架到插件市场

本插件已提交至 ClassIsland 插件市场索引，相关清单见仓库根目录的 [`rspci.jimmyxiao.yml`](./rspci.jimmyxiao.yml)（该文件不打包进 `.cipx`，仅用于提交到 [PluginIndex](https://github.com/ClassIsland/PluginIndex/tree/main/index/plugins-v2)）。

---

## 项目结构

```
RemoteShutdownPlugin/
├── Plugin.cs                               # 插件入口，注册服务与设置页
├── manifest.yml                            # 插件元数据（打包进 cipx）
├── rspci.jimmyxiao.yml                     # 插件市场索引清单（提交到 PluginIndex 用）
├── index.html                              # 嵌入式网页控制面板
├── icon.ico                                # 插件图标（Win11/Metro 风格，多分辨率）
├── Models/
│   └── PluginSettings.cs                   # 配置模型（端口/用户名/密码/自动启动）
├── Services/
│   ├── RemoteControlService.cs             # IHostedService，管理服务器生命周期
│   └── WebServer.cs                        # HttpListener HTTP 服务器 + API 处理
├── ViewModels/
│   └── RemoteControlViewModel.cs           # 设置页 ViewModel（INPC + 命令）
└── Views/SettingsPages/
    ├── RemoteControlSettingsPage.axaml     # Avalonia 设置页面布局
    └── RemoteControlSettingsPage.axaml.cs  # Code-behind
```

---

## API 接口

服务器启动后暴露以下接口（均需 Basic Auth）：

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/` | 网页控制面板 |
| `GET` | `/screenshot` | 截图，返回 `{"success":true,"image":"<base64 JPEG>"}` |
| `POST` | `/shutdown` | 关机，返回 `{"success":true,"message":"..."}` |
| `POST` | `/reboot` | 重启，返回 `{"success":true,"message":"..."}` |


