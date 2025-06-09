# Lingualink Client 改进测试指南

## 已实施的改进

### 1. 修复"测试连接"按钮 ✅
- **位置**: AccountPage.xaml 和 AccountPageViewModel.cs
- **功能**: 点击按钮会调用API的 `/health` 端点验证连接
- **测试方法**: 
  1. 打开账户页面
  2. 输入服务器URL（如：http://localhost:8080/api/v1）
  3. 点击"测试连接"按钮
  4. 应该显示连接成功或失败的消息

### 2. 简化UI界面 ✅
- **位置**: IndexPage.xaml 和 TranslationResultViewModel.cs
- **改动**: 移除了"服务器原始响应"文本框，只保留"VRChat输出"
- **效果**: 界面更简洁，用户专注于最终输出结果
- **测试方法**: 
  1. 打开主页面
  2. 确认翻译结果区域只显示一个文本框（VRChat输出）
  3. 进行语音翻译测试，确认结果正确显示

### 3. 优化原文调用 ✅
- **位置**: Models/Models.cs (TemplateProcessor类)
- **功能**: 在可用占位符列表中添加了 "Source Text ({transcription})"
- **测试方法**:
  1. 打开消息模板页面
  2. 查看可用占位符列表，应该看到 "Source Text ({transcription})" 选项
  3. 创建一个只包含 `{transcription}` 的模板
  4. 测试语音翻译，确认原文正确显示

### 4. 实现"仅转录"模式 ✅
- **位置**: 
  - ILingualinkApiService.cs (接口更新)
  - LingualinkApiService.cs (实现更新)
  - AudioTranslationOrchestrator.cs (智能判断逻辑)
- **功能**: 当模板只包含 `{transcription}` 等原文占位符时，自动切换为"transcribe"任务
- **测试方法**:
  1. 创建一个只包含原文的模板，例如：`原文: {transcription}`
  2. 启用自定义模板
  3. 进行语音翻译
  4. 检查日志，应该看到 "Setting task to 'transcribe'" 消息
  5. 确认只返回转录结果，没有翻译

## 测试场景

### 场景1: 测试连接功能
```
1. 打开账户页面
2. 输入正确的服务器URL
3. 点击"测试连接" - 应该成功
4. 输入错误的URL
5. 点击"测试连接" - 应该失败并显示错误信息
```

### 场景2: 仅转录模式
```
模板内容: "我说了: {transcription}"
预期行为: 
- API调用使用 task="transcribe"
- 只返回原文，不进行翻译
- 日志显示切换到转录模式的消息
```

### 场景3: 混合模式
```
模板内容: "原文: {transcription}\n英文: {en}\n日文: {ja}"
预期行为:
- API调用使用 task="translate" 
- 返回原文和翻译结果
- 所有占位符都被正确替换
```

### 场景4: 纯翻译模式
```
模板内容: "EN: {en}\nJA: {ja}"
预期行为:
- API调用使用 task="translate"
- 返回翻译结果
- 不显示原文
```

## 验证要点

1. **UI简化**: 主页面只显示一个结果文本框
2. **连接测试**: 按钮功能正常，能正确验证API连接
3. **智能任务选择**: 根据模板内容自动选择transcribe或translate
4. **占位符支持**: {transcription}占位符在模板中可用且工作正常
5. **向后兼容**: 现有模板和功能不受影响

## 注意事项

- 确保服务器运行在正确的端点
- 检查API密钥配置正确
- 观察日志输出以确认任务类型选择正确
- 测试不同的模板组合以验证智能判断逻辑
