# 客户端 Checkout 集成方案 — Windows .NET (WebView2)

> 更新时间：2026-03-02
> 状态：Active
> 适用范围：Windows .NET 桌面客户端集成 AuthServer Checkout 页面

---

## 1. 概述

客户端原有的"内置付费界面"迁移为打开 AuthServer 提供的 Web Checkout 页面。
客户端通过 **WebView2** 弹窗承载该页面，通过 `postMessage` 协议实现双向通信。

### 交互流程

```
用户点击"订阅"
    │
    ▼
客户端获取 access_token（已有登录态）
    │
    ▼
打开 WebView2 窗口
  URL: https://{AUTH_HOST}/checkout?token={access_token}
    │
    ▼
Checkout 页面加载 → 选套餐 → 选时长 → 扫码支付
    │
    │  支付成功 → postMessage({ type: "lingualink_checkout_paid", ... })
    │  支付成功 → 3 秒后 window.close()
    ▼
客户端收到消息 → 刷新订阅状态 → 关闭弹窗
```

---

## 2. 环境要求

| 依赖 | 版本 | 说明 |
|------|------|------|
| `Microsoft.Web.WebView2` | ≥ 1.0.2210 | NuGet 包 |
| WebView2 Runtime | Edge Chromium 基础 | Windows 10/11 自带；可用 Evergreen 引导安装 |
| .NET | 6.0+ 或 .NET Framework 4.6.2+ | WPF 或 WinForms 均可 |

---

## 3. 实现步骤

### 3.1 NuGet 引用

```xml
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2903.40" />
```

### 3.2 XAML 窗口定义（WPF 示例）

```xml
<!-- CheckoutWindow.xaml -->
<Window x:Class="LinguaLink.Client.Views.CheckoutWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"
        Title="LinguaLink — 订阅服务"
        Width="1120" Height="780"
        WindowStartupLocation="CenterOwner"
        ResizeMode="CanResize">
    <Grid>
        <wv2:WebView2 x:Name="CheckoutWebView"
                      DefaultBackgroundColor="Transparent" />
    </Grid>
</Window>
```

### 3.3 窗口后端代码

```csharp
// CheckoutWindow.xaml.cs
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace LinguaLink.Client.Views;

public partial class CheckoutWindow : Window
{
    /// <summary>
    /// 支付成功后触发，携带 out_trade_no 用于客户端刷新订阅。
    /// </summary>
    public event Action<string>? PaymentCompleted;

    private readonly string _accessToken;
    private readonly string _authHost;

    public CheckoutWindow(string accessToken, string authHost = "http://localhost:9080")
    {
        InitializeComponent();
        _accessToken = accessToken;
        _authHost = authHost.TrimEnd('/');

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 1. 初始化 WebView2 环境
        var env = await CoreWebView2Environment.CreateAsync(
            userDataFolder: System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LinguaLink", "WebView2Cache"
            )
        );
        await CheckoutWebView.EnsureCoreWebView2Async(env);

        var webview = CheckoutWebView.CoreWebView2;

        // 2. 监听 postMessage（Web → 客户端）
        webview.WebMessageReceived += OnWebMessageReceived;

        // 3. 监听页面发起的 window.close()
        webview.WindowCloseRequested += (_, _) =>
        {
            Dispatcher.Invoke(Close);
        };

        // 4. 打开 Checkout 页面，通过 URL query 传递 token
        var checkoutUrl = $"{_authHost}/checkout?token={Uri.EscapeDataString(_accessToken)}";
        webview.Navigate(checkoutUrl);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var json = args.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 检查消息类型
            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var type = typeProp.GetString();

            switch (type)
            {
                case "lingualink_checkout_paid":
                {
                    var outTradeNo = root.TryGetProperty("out_trade_no", out var tn)
                        ? tn.GetString() ?? ""
                        : "";

                    Dispatcher.Invoke(() =>
                    {
                        PaymentCompleted?.Invoke(outTradeNo);
                        // 页面会在 3 秒后 window.close()，也可以主动关闭
                        Close();
                    });
                    break;
                }

                // 预留其他消息类型扩展
                default:
                    break;
            }
        }
        catch
        {
            // 忽略非 JSON 或无关消息
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        CheckoutWebView.CoreWebView2?.WebMessageReceived -= OnWebMessageReceived!;
        CheckoutWebView.Dispose();
    }
}
```

### 3.4 调用方式

```csharp
// 在主窗口的"订阅"按钮事件中调用
private void OnSubscribeClicked(object sender, RoutedEventArgs e)
{
    // accessToken 来自登录流程中获取的 JWT
    var accessToken = _authService.CurrentAccessToken;
    if (string.IsNullOrEmpty(accessToken))
    {
        MessageBox.Show("请先登录后再订阅。");
        return;
    }

    var checkout = new CheckoutWindow(accessToken, App.AuthServerHost)
    {
        Owner = this  // 保持为模态父窗口
    };

    checkout.PaymentCompleted += async (outTradeNo) =>
    {
        // 支付成功：刷新本地订阅状态
        await _subscriptionService.RefreshCurrentSubscription();
        // 可选：显示通知
        ShowToast($"订阅成功！订单号：{outTradeNo}");
    };

    checkout.ShowDialog(); // 模态方式打开
}
```

---

## 4. 通信协议

### 4.1 客户端 → 页面

**方式一：URL Query（推荐）**

打开 WebView 时将 token 放在 URL 中：
```
https://{AUTH_HOST}/checkout?token={access_token}
```

**方式二：postMessage（备用）**

如果 token 需要延迟传入，可在 WebView 加载完成后通过 JS 注入：
```csharp
await webview.ExecuteScriptAsync(
    $"window.postMessage({{ type: 'lingualink_checkout_token', token: '{accessToken}' }}, '*')"
);
```

### 4.2 页面 → 客户端

支付成功后，页面通过 `window.parent.postMessage` 发送：

```json
{
  "type": "lingualink_checkout_paid",
  "out_trade_no": "LL20260302150000ABCD1234"
}
```

WebView2 通过 `WebMessageReceived` 事件接收此消息。

> **注意**：Checkout 页面还会在发送成功消息 3 秒后调用 `window.close()`，
> 触发 WebView2 的 `WindowCloseRequested` 事件。客户端可在此事件中关闭弹窗窗口。

---

## 5. 补充处理

### 5.1 WebView2 Runtime 通用性

```csharp
// App.xaml.cs — 启动时检查 WebView2 Runtime 是否安装
private void CheckWebView2Runtime()
{
    try
    {
        var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
        // 已安装，正常启动
    }
    catch
    {
        // 引导用户下载 Evergreen Bootstrapper
        // https://developer.microsoft.com/en-us/microsoft-edge/webview2/
        var result = MessageBox.Show(
            "本应用需要 WebView2 Runtime 组件才能使用订阅功能。是否前往下载？",
            "缺少 WebView2",
            MessageBoxButton.YesNo
        );
        if (result == MessageBoxResult.Yes)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                UseShellExecute = true
            });
        }
    }
}
```

### 5.2 Token 安全

- **不要持久化 token 到磁盘**。仅在内存中持有，通过 URL query 一次性传递。
- WebView2 的 `userDataFolder` 使用独立路径，避免与系统浏览器共享 cookie/缓存。
- 窗口关闭时 WebView2 会随之销毁，页面内存中的 token 自动释放。

### 5.3 网络错误处理

```csharp
webview.NavigationCompleted += (_, args) =>
{
    if (!args.IsSuccess)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                "无法加载支付页面，请检查网络连接后重试。",
                "网络错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            Close();
        });
    }
};
```

### 5.4 生产环境 URL

| 环境 | AUTH_HOST |
|------|-----------|
| 开发 | `http://localhost:9080` |
| 生产 | `https://auth.lingualink.com`（API 直接服务 `/checkout` 静态文件） |

生产环境中 Go API 在 release mode 下自动托管 `/checkout` 路由（见 `router.go`），
无需额外部署前端服务器。

---

## 6. 删除旧代码

迁移完成后，应删除客户端中以下旧代码：

- 旧的内置套餐 UI 界面（XAML + ViewModel）
- 旧的套餐 API 调用逻辑（可保留用于非支付场景的套餐查询）
- 旧的二维码生成逻辑
- 旧的支付状态轮询逻辑

这些功能现在全部由 Web Checkout 页面承担。

---

## 7. 验收标准

- [ ] `CheckoutWindow` 能正常打开并加载 Checkout 页面
- [ ] 页面正确显示套餐列表（从 API 获取）
- [ ] 用户选择套餐后可生成支付宝二维码
- [ ] 支付成功后客户端收到 `lingualink_checkout_paid` 消息
- [ ] 客户端正确刷新订阅状态
- [ ] 弹窗在支付成功后自动关闭
- [ ] WebView2 Runtime 缺失时有友好提示
- [ ] 网络异常时有错误处理
