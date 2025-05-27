# LinguaLink Client - 新后端支持迁移指南

## 概述

LinguaLink Client 已更新以支持新的 LinguaLink Server 后端，该后端提供了增强的 API 鉴权系统和改进的翻译功能。

## 主要变化

### 1. API 端点更新
- **新端点**: `http://localhost:5000/api/v1/translate_audio`

### 2. API 鉴权系统
新后端支持 API 密钥鉴权，提供更安全的访问控制。

#### 支持的鉴权方式
- **X-API-Key 头部** (推荐): `X-API-Key: your-api-key`
- **Authorization Bearer 头部**: `Authorization: Bearer your-api-key`

#### API 密钥格式
- API 密钥以 `lls_` 开头
- 示例: `lls_1234567890abcdef`

### 3. 新增请求字段
- **user_prompt**: 系统发送固定提示，用于指导 AI 处理音频
- **target_languages**: 支持多个目标语言的数组形式

## 客户端配置

### 服务页面新增配置项

在"服务"页面中，新增了"API 认证"配置区域：

1. **启用认证**: 开启/关闭 API 密钥认证
2. **API 密钥**: 输入从后端获取的 API 密钥

### 默认配置
- **服务器 URL**: `http://localhost:5000/api/v1/translate_audio`
- **启用认证**: `true` (默认启用)
- **API 密钥**: 空 (需要用户配置)
- **系统提示**: `"请处理下面的音频。"` (固定值)

## 配置步骤

### 1. 获取 API 密钥

首先从 LinguaLink Server 获取 API 密钥：

```bash
# 使用现有密钥生成新密钥
curl -X POST "http://localhost:5000/api/v1/auth/generate_key" \
  -H "X-API-Key: your-existing-key" \
  -d "name=lingualink-client&expires_in_days=30"

# 或使用管理工具生成
python -m src.lingualink.utils.key_generator --name "lingualink-client"
```

### 2. 配置客户端

1. 打开 LinguaLink Client
2. 进入"服务"页面
3. 在"API 认证"区域：
   - 确保"启用认证"已勾选
   - 在"API 密钥"字段输入获取的密钥

### 3. 验证配置

保存设置后，客户端将：
- 自动使用新的 API 端点
- 在请求中包含 API 密钥
- 在请求中包含固定的用户提示
## 故障排除

### 常见问题

1. **认证失败**
   - 检查 API 密钥格式是否正确
   - 确认密钥在后端是否有效
   - 验证密钥是否已过期

2. **连接失败**
   - 检查服务器 URL 是否正确
   - 确认后端服务是否运行
   - 验证网络连接

3. **翻译失败**
   - 检查目标语言设置
   - 验证音频格式是否支持

### 调试建议

1. 查看日志页面的详细错误信息
2. 使用 curl 测试 API 端点连通性
3. 检查后端日志确认请求是否到达

## API 使用示例

### 手动测试 API

```bash
# 测试健康检查
curl -H "X-API-Key: your-api-key" \
  "http://localhost:5000/api/v1/health"

# 测试音频翻译
curl -X POST "http://localhost:5000/api/v1/translate_audio" \
  -H "X-API-Key: your-api-key" \
  -F "audio_file=@test.wav" \
  -F "user_prompt=请处理下面的音频。" \
  -F "target_languages=英文" \
  -F "target_languages=日文"
```

### 响应格式

```json
{
  "status": "success",
  "duration_seconds": 2.5,
  "data": {
    "raw_text": "原文：你好世界\n英文：Hello World\n日文：こんにちは世界",
    "原文": "你好世界",
    "英文": "Hello World", 
    "日文": "こんにちは世界"
  }
}
```

## 安全建议

1. **密钥管理**
   - 定期轮换 API 密钥
   - 不要在公共场所暴露密钥
   - 为不同用途使用不同密钥

2. **网络安全**
   - 在生产环境中使用 HTTPS
   - 限制后端服务的网络访问
   - 监控 API 使用情况

## 更新日志

### v2.0.0 (2024-01-XX)
- 添加 API 密钥鉴权支持
- 更新 API 端点到 v1
- 改进错误处理和日志记录
- 支持新的后端响应格式

---

如有问题或需要支持，请查看完整的后端文档或联系开发团队。 