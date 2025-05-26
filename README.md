# LinguaLink Client

一个基于 WPF 的实时语音翻译客户端，支持 VRChat OSC 集成。

## 功能特性

- 🎤 **实时语音识别**: 自动检测和处理语音输入
- 🌍 **多语言翻译**: 支持英文、日文、中文等多种语言翻译
- 🔐 **API 密钥认证**: 支持安全的后端 API 认证
- 🎮 **VRChat 集成**: 直接发送翻译结果到 VRChat 聊天框
- 📝 **自定义模板**: 灵活的消息格式模板系统
- 🎛️ **音频参数调节**: 可调节的 VAD (语音活动检测) 参数
- 📊 **实时日志**: 详细的运行状态和错误日志
- 🌐 **多语言界面**: 支持中文和英文界面

## 系统要求

- Windows 10/11
- .NET 8.0 Runtime
- 麦克风设备
- LinguaLink Server 后端

## 快速开始

### 1. 安装运行时

确保系统已安装 .NET 8.0 Runtime：
- 下载：[.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### 2. 配置后端

启动 LinguaLink Server 并获取 API 密钥：

```bash
# 生成 API 密钥
python -m src.lingualink.utils.key_generator --name "lingualink-client"

# 启动服务器
python3 manage.py start
```

### 3. 配置客户端

1. 启动 LinguaLink Client
2. 进入"服务"页面
3. 配置以下设置：
   - **服务器 URL**: `http://localhost:5000/api/v1/translate_audio`
   - **启用认证**: 勾选
   - **API 密钥**: 输入从后端获取的密钥
   - **用户提示**: 自定义 AI 处理指令（可选）

### 4. 开始使用

1. 在"启动"页面选择麦克风设备
2. 点击"开始工作"开始语音监听
3. 说话时系统会自动识别并翻译
4. 翻译结果会显示在界面上，并可发送到 VRChat

## 主要功能

### 语音识别与翻译

- **自动语音检测**: 智能识别语音开始和结束
- **多语言支持**: 支持英文、日文、中文、韩文、法文等
- **实时处理**: 语音结束后立即开始翻译
- **模板系统**: 支持自定义输出格式

### VRChat 集成

- **OSC 协议**: 通过 OSC 协议与 VRChat 通信
- **聊天框集成**: 直接发送翻译结果到游戏聊天框
- **参数配置**: 可配置发送方式和通知音效

### 界面功能

- **多页面布局**: 启动、服务配置、日志查看等
- **实时状态**: 显示当前工作状态和进度
- **设置保存**: 所有配置自动保存
- **多语言**: 支持中文和英文界面切换

## 配置说明

### API 认证设置

- **启用认证**: 是否使用 API 密钥认证
- **API 密钥**: 从后端获取的认证密钥
- **用户提示**: 发送给 AI 的处理指令

### 音频参数

- **静音检测阈值**: 检测语音结束的静音时长
- **最小语音时长**: 有效语音的最短时间
- **最大语音时长**: 单次录音的最长时间
- **音量阈值**: 开始录音的最小音量

### OSC 设置

- **IP 地址**: VRChat 客户端的 IP 地址
- **端口**: OSC 通信端口 (默认 9000)
- **立即发送**: 是否绕过键盘直接发送
- **通知音效**: 是否播放发送提示音

## 模板系统

### 预设模板

- **完整文本**: 显示服务器返回的完整内容
- **英文+日文**: 只显示英文和日文翻译
- **三语对照**: 显示英文、日文、中文
- **自定义格式**: 带标签的格式化显示

### 自定义模板

支持使用占位符创建自定义模板：

```
{英文}
{日文}
{中文}
```

模板示例：
```
English: {英文}
Japanese: {日文}
Chinese: {中文}
```

## 故障排除

### 常见问题

1. **无法连接后端**
   - 检查服务器 URL 是否正确
   - 确认后端服务是否运行
   - 验证网络连接

2. **认证失败**
   - 检查 API 密钥是否正确
   - 确认密钥未过期
   - 验证认证设置是否启用

3. **麦克风无法使用**
   - 检查麦克风权限
   - 尝试刷新设备列表
   - 确认设备是否被其他程序占用

4. **VRChat 无法接收消息**
   - 确认 VRChat 启用了 OSC
   - 检查 IP 地址和端口设置
   - 验证防火墙设置

### 调试信息

- 查看"日志"页面获取详细错误信息
- 检查后端日志确认请求处理状态
- 使用网络工具测试 API 连通性

## 开发

### 环境要求

- Visual Studio 2022 或 JetBrains Rider
- .NET 8.0 SDK
- Windows 10 SDK

### 构建项目

```bash
# 克隆项目
git clone <repository-url>
cd Lingualink_Client

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行项目
dotnet run
```

### 项目结构

```
Lingualink_Client/
├── Models/              # 数据模型
├── ViewModels/          # MVVM 视图模型
├── Views/               # XAML 视图
├── Services/            # 业务逻辑服务
├── Properties/          # 本地化资源
├── Assets/              # 资源文件
└── docs/                # 文档
```

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！

## 相关项目

- [LinguaLink Server](https://github.com/your-org/lingualink-server) - 后端服务
- [VRChat OSC Documentation](https://docs.vrchat.com/docs/osc-overview) - VRChat OSC 文档

## 更新日志

### v2.0.0 (2024-01-XX)
- 🔐 添加 API 密钥认证支持
- 🔄 更新 API 端点到 v1
- ⚙️ 增加用户提示配置
- 🐛 改进错误处理和日志记录
- 🌐 支持新的后端响应格式

### v1.0.0
- 🎉 初始版本发布
- 🎤 基础语音识别和翻译功能
- 🎮 VRChat OSC 集成
- 📝 模板系统
- 🌍 多语言界面支持

---

如有问题或需要支持，请查看 [API 迁移指南](docs/API_Migration_Guide.md) 或提交 Issue。 