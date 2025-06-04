# LinguaLink Client - 新API适配总结

## 概述

本次更新将LinguaLink Client适配到新的LinguaLink Core API v2.0，实现了更好的性能、功能和多语言支持。

## 主要更改

### 1. API端点更新
- **旧端点**: `/translate_audio` (multipart form data)
- **新端点**: `/process_audio` (JSON format)
- **支持任务类型**: 
  - `transcribe`: 仅转录音频为文本
  - `translate`: 转录音频并翻译为目标语言

### 2. 语言系统重构
- **旧系统**: 使用中文语言名称（如"英文"、"日文"）
- **新系统**: 使用标准语言代码（如"en"、"ja"）
- **映射支持**: 自动在中文名称和语言代码之间转换
- **支持的语言代码**:
  - `en` - 英文
  - `ja` - 日文  
  - `fr` - 法文
  - `zh` - 中文
  - `ko` - 韩文
  - `es` - 西班牙文
  - `ru` - 俄文
  - `de` - 德文
  - `it` - 意大利文

### 3. 响应格式适配
- **新响应结构**: 
  - `transcription`: 原文转录结果
  - `translations`: 语言代码到翻译文本的映射
  - `raw_response`: 完整的LLM响应
  - `processing_time`: 处理时间
  - `metadata`: 处理元数据
- **向后兼容**: 自动转换新API响应为旧格式，保持现有UI兼容性

### 4. 配置选项新增
- **UseNewApiFormat**: 启用/禁用新API格式（默认启用）
- **TaskType**: 选择任务类型（"transcribe" 或 "translate"）
- **默认服务器URL**: 更新为 `http://localhost:8080/api/v1/`

### 5. 多语言UI增强
- **日语支持**: 添加了新API相关的日语字符串
- **中文支持**: 添加了新API相关的中文字符串
- **英文支持**: 添加了新API相关的英文字符串

## 技术实现

### 1. Models.cs 更新
- 添加了 `NewApiResponse` 和 `ApiMetadata` 类
- 保持了 `ServerResponse` 的向后兼容性
- 支持新的JSON属性映射

### 2. LanguageDisplayHelper.cs 增强
- 添加了语言代码映射字典
- 实现了双向转换方法：
  - `ConvertChineseNameToLanguageCode()`
  - `ConvertLanguageCodeToChineseName()`
  - `ConvertChineseNamesToLanguageCodes()`
  - `ConvertLanguageCodesToChineseNames()`

### 3. TranslationService.cs 重构
- 实现了双API支持：
  - `TranslateAudioSegmentNewApiAsync()`: 新API实现
  - `TranslateAudioSegmentLegacyApiAsync()`: 旧API实现
- 自动路由到适当的API版本
- 添加了响应格式转换逻辑

### 4. AppSettings.cs 扩展
- 添加了 `TaskType` 配置
- 添加了 `UseNewApiFormat` 配置
- 更新了默认服务器URL

## 使用方法

### 1. 启用新API格式
在设置中确保 `UseNewApiFormat` 为 `true`（默认启用）

### 2. 选择任务类型
- **仅转录**: 设置 `TaskType` 为 `"transcribe"`
- **转录和翻译**: 设置 `TaskType` 为 `"translate"`（默认）

### 3. 配置服务器
- 确保服务器URL指向新的API端点
- 默认: `http://localhost:8080/api/v1/`

## 向后兼容性

- **旧API支持**: 可以通过设置 `UseNewApiFormat = false` 继续使用旧API
- **UI兼容性**: 所有现有UI组件无需修改
- **配置兼容性**: 现有配置文件自动升级

## 测试建议

1. **基本功能测试**:
   - 测试音频转录功能
   - 测试音频翻译功能
   - 验证多语言输出

2. **API切换测试**:
   - 测试新API格式
   - 测试旧API格式回退
   - 验证响应格式转换

3. **多语言UI测试**:
   - 切换到日语界面
   - 验证新增的UI字符串
   - 测试语言代码映射

## 故障排除

### 常见问题
1. **连接失败**: 检查服务器URL是否正确
2. **认证失败**: 验证API密钥配置
3. **翻译失败**: 确认目标语言设置正确

### 调试建议
1. 查看日志页面的详细错误信息
2. 检查服务器是否支持新API格式
3. 尝试切换到旧API格式进行对比测试

## 下一步

1. 测试新API功能
2. 根据需要调整配置
3. 反馈使用体验和问题
