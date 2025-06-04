# 追加录音逻辑实施文档

## 概述

本文档描述了在 Lingualink Client 中实施的追加录音逻辑，该功能完全替代了原有的"静音检测阈值"逻辑，通过固定时长的追加录音来确保尾音被完整捕捉。

## 核心逻辑

### 传统逻辑 vs 新逻辑

**传统逻辑（已移除）**：
- 基于 `SilenceThresholdSeconds` 参数
- 在检测到静音持续一定时间后才截断发送
- 可能错过轻微的尾音

**新逻辑（追加录音）**：
- 基于 `PostSpeechRecordingDurationSeconds` 参数
- VAD 判断语音结束后立即进入固定时长的追加录音阶段
- 在追加录音期间如果检测到新语音，则重置状态继续录制
- 确保尾音被完整捕捉

### 状态机设计

保持简洁的状态机，不显示追加录音状态：

```
Idle → Listening → SpeechDetected → Processing → Listening
                                 ↗            ↙
                            (检测到新语音时重置)
```

**注意**：追加录音在后台进行，但UI状态保持为 `SpeechDetected`，避免状态显示混乱。

## 技术实现

### 1. 配置参数更改

**AppSettings.cs**：
- 移除：`SilenceThresholdSeconds`
- 新增：`PostSpeechRecordingDurationSeconds` (默认 0.5 秒)

### 2. AudioService.cs 核心变更

**新增状态变量**：
```csharp
private bool _isPostRecordingActive = false;
private DateTime _postRecordingShouldEndTime;
```

**OnVadDataAvailable 方法重写**：
- **情况1：检测到有效语音**
  - 如果是新语音开始：设置为 `SpeechDetected` 状态
  - 如果在追加录音期间：重置追加录音状态，保持 `SpeechDetected` 状态
  - 继续累积音频数据
  - 检查最大时长限制

- **情况2：未检测到有效语音**
  - 如果之前正在说话：进入追加录音阶段
  - 设置追加录音结束时间
  - **保持 `SpeechDetected` 状态**（不显示追加录音状态）
  - 继续累积音频数据（包括静音）

**最小录音时长修正**：
- 实际最小时长 = 原始最小时长 + 追加录音时长
- 避免短语音被强制发送的问题

**新方法**：
- `CheckAndFinalizeSegmentIfNeeded()`: 替代原有的 `CheckSilenceTimeout()`
- `ProcessAndSendSegment()`: 统一的片段处理和发送逻辑

### 3. UI 界面更新

**ServicePage.xaml**：
- 将"静音检测阈值"替换为"追加录音时长"
- 更新绑定到新的属性

**ServicePageViewModel.cs**：
- 属性重命名：`SilenceThresholdSeconds` → `PostSpeechRecordingDurationSeconds`
- 更新所有相关的加载、保存和验证逻辑

### 4. 本地化支持

**新增本地化字符串**：
- `PostSpeechRecordingDuration`: "追加录音时长 (秒)" / "Post-Speech Recording Duration (seconds)"
- `AudioStatusPostRecording`: "正在追加录音..." / "Recording tail audio..."
- `ValidationPostSpeechRecordingDurationInvalid`: 验证错误消息

## 工作流程详解

### 正常语音处理流程

1. **语音开始**：VAD 检测到语音 → `SpeechDetected` 状态
2. **语音持续**：继续累积音频数据
3. **VAD 判断结束**：连续几帧未检测到语音 → 进入 `PostRecording` 状态
4. **追加录音**：固定时长（如 0.5 秒）继续录制
5. **时间到达**：处理并发送完整音频片段 → 回到 `Listening` 状态

### 短暂停顿处理流程

1. **语音开始**：`SpeechDetected` 状态
2. **短暂停顿**：VAD 误判为结束 → 进入 `PostRecording` 状态
3. **继续说话**：在追加录音期间检测到新语音
4. **状态重置**：取消追加录音，回到 `SpeechDetected` 状态
5. **继续录制**：作为同一个音频片段继续处理

### 最大时长限制

无论处于何种状态，当音频片段达到 `MaxVoiceDurationSeconds` 时：
- 立即强制分割发送
- 重置所有状态
- 开始新的录制周期

## 优势分析

### 1. 更可靠的尾音捕捉
- 固定时长的追加录音确保轻微尾音被录制
- 不依赖于静音检测的准确性

### 2. 对短暂停顿的容错性
- 在追加录音期间检测到新语音会自动重置
- 避免将连续语音错误分割

### 3. 逻辑清晰简单
- 主要判断条件变为追加录音是否到期
- 减少了复杂的静音时长计算

### 4. 参数调整简单
- 只需调整一个时长参数（0.3-0.5 秒通常效果最佳）
- 不需要复杂的静音阈值调优

## 配置建议

### PostSpeechRecordingDurationSeconds 推荐值
- **0.3 秒**：适合快速对话，减少延迟
- **0.5 秒**：平衡选择，适合大多数场景（默认）
- **0.7 秒**：适合慢速语音或需要确保完整性的场景

### 注意事项
1. **VAD 灵敏度**：依然依赖 WebRtcVad 的判断准确性
2. **网络延迟**：追加录音会增加少量处理延迟
3. **噪声环境**：背景噪声可能影响 VAD 判断

## 测试建议

1. **正常语音测试**：测试各种语速和音量的语音
2. **停顿测试**：测试短暂停顿后继续说话的情况
3. **尾音测试**：测试轻声结尾的语音是否被完整捕捉
4. **噪声环境测试**：在有背景噪声的环境中测试
5. **参数调优**：根据实际使用情况调整追加录音时长

## 问题修复说明

### 修复的问题

1. **最小录音时长失效**
   - **问题**：任何语音都被强制追加0.5秒，导致总是超过最小时长被发送
   - **修复**：实际最小时长 = 原始最小时长 + 追加录音时长，确保短语音不会被错误发送

2. **状态显示异常**
   - **问题**：一旦进入追加录音状态就卡住，即使检测到新语音也不恢复
   - **修复**：移除 `PostRecording` 状态，追加录音期间保持 `SpeechDetected` 状态

3. **UI 状态混乱**
   - **问题**：追加录音状态显示画蛇添足，用户体验不佳
   - **修复**：追加录音在后台进行，UI 状态保持简洁清晰

### 修复后的逻辑

```csharp
// 计算实际的最小录音时长要求
double effectiveMinDuration = _minVoiceDurationSeconds + _postSpeechRecordingDurationSeconds;

// 只有满足实际最小时长才发送
if (segmentDurationSeconds >= effectiveMinDuration) {
    // 发送音频片段
}
```

## 兼容性说明

此更改完全替代了原有的静音检测逻辑，不保持向后兼容性。现有的配置文件中的 `SilenceThresholdSeconds` 参数将被忽略，需要重新配置 `PostSpeechRecordingDurationSeconds` 参数。
