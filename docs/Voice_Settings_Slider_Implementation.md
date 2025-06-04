# 语音设置滑块界面实施文档

## 概述

本文档描述了将语音设置参数从文本框改为滑块界面的实施，包括参数范围限制和用户体验优化。

## 实施的改进

### 1. UI 界面升级

**从文本框改为滑块 + 文本框组合**：
- 滑块提供直观的拖拽调节
- 文本框显示精确数值
- 实时双向绑定，滑块和文本框同步更新

### 2. 参数范围限制

**追加录音时长 (PostSpeechRecordingDurationSeconds)**：
- 范围：0.1 - 0.7 秒
- 步进：0.05 秒（小步进），0.1 秒（大步进）
- 刻度间隔：0.1 秒
- 默认值：0.5 秒

**最小语音时长 (MinVoiceDurationSeconds)**：
- 范围：0.1 - 0.7 秒
- 步进：0.05 秒（小步进），0.1 秒（大步进）
- 刻度间隔：0.1 秒
- 默认值：0.5 秒

**最大语音时长 (MaxVoiceDurationSeconds)**：
- 范围：1 - 10 秒
- 步进：0.5 秒（小步进），1 秒（大步进）
- 刻度间隔：1 秒
- 默认值：10 秒

### 3. 自动验证和限制

**ViewModel 属性验证回调**：
```csharp
partial void OnPostSpeechRecordingDurationSecondsChanged(double oldValue, double newValue)
{
    if (newValue < 0.1 || newValue > 0.7)
    {
        _postSpeechRecordingDurationSeconds = Math.Clamp(newValue, 0.1, 0.7);
        OnPropertyChanged(nameof(PostSpeechRecordingDurationSeconds));
    }
}
```

## UI 布局设计

### 滑块组合控件结构

每个语音参数都使用统一的布局：

```xml
<Grid Margin="0,0,0,10">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />  <!-- 标签 -->
        <RowDefinition Height="Auto" />  <!-- 滑块+文本框 -->
        <RowDefinition Height="Auto" />  <!-- 提示文本 -->
    </Grid.RowDefinitions>

    <Label Grid.Row="0" Content="{Binding Label}" />
    <Grid Grid.Row="1">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />     <!-- 滑块 -->
            <ColumnDefinition Width="Auto" />  <!-- 文本框 -->
        </Grid.ColumnDefinitions>
        <Slider Grid.Column="0" ... />
        <ui:TextBox Grid.Column="1" Width="60" ... />
    </Grid>
    <ui:TextBlock Grid.Row="2" Text="{Binding Hint}" ... />
</Grid>
```

### 视觉特性

- **滑块宽度**：自适应容器宽度
- **文本框宽度**：固定 60 像素
- **提示文本**：10 号字体，斜体，灰色
- **间距**：各控件间 10 像素间距

## 本地化支持

### 新增提示文本

**中文提示**：
- `PostSpeechRecordingDurationHint`: "VAD判断语音结束后继续录制的时长，用于捕捉尾音。范围：0.1-0.7秒，推荐：0.5秒。"
- `MinVoiceDurationHint`: "语音片段的最小时长要求。短于此时长的语音将被忽略。范围：0.1-0.7秒，推荐：0.5秒。"
- `MaxVoiceDurationHint`: "单个语音片段的最大时长限制。超过此时长将强制分割。范围：1-10秒，推荐：10秒。"

**英文提示**：
- `PostSpeechRecordingDurationHint`: "Duration to continue recording after VAD detects speech end, for capturing tail sounds. Range: 0.1-0.7s, Recommended: 0.5s."
- `MinVoiceDurationHint`: "Minimum duration requirement for voice segments. Shorter segments will be ignored. Range: 0.1-0.7s, Recommended: 0.5s."
- `MaxVoiceDurationHint`: "Maximum duration limit for single voice segments. Longer segments will be force-split. Range: 1-10s, Recommended: 10s."

### 更新验证消息

**中文验证消息**：
- `ValidationPostSpeechRecordingDurationInvalid`: "追加录音时长必须在0.1秒到0.7秒之间。"
- `ValidationMinVoiceDurationInvalid`: "最小语音时长必须在0.1秒到0.7秒之间。"
- `ValidationMaxVoiceDurationInvalid`: "最大语音时长必须在1秒到10秒之间。"

**英文验证消息**：
- `ValidationPostSpeechRecordingDurationInvalid`: "Post-speech recording duration must be between 0.1 and 0.7 seconds."
- `ValidationMinVoiceDurationInvalid`: "Minimum voice duration must be between 0.1 and 0.7 seconds."
- `ValidationMaxVoiceDurationInvalid`: "Maximum voice duration must be between 1 and 10 seconds."

## 技术实现细节

### 1. 数据绑定

**双向绑定**：
```xml
<Slider Value="{Binding PostSpeechRecordingDurationSeconds, UpdateSourceTrigger=PropertyChanged}" />
<ui:TextBox Text="{Binding PostSpeechRecordingDurationSeconds, UpdateSourceTrigger=PropertyChanged, StringFormat=F2}" />
```

**格式化显示**：
- 追加录音时长和最小语音时长：`StringFormat=F2`（显示两位小数）
- 最大语音时长：`StringFormat=F1`（显示一位小数）

### 2. 属性验证

**自动范围限制**：
- 用户输入超出范围时自动校正到边界值
- 实时触发 `OnPropertyChanged` 更新 UI
- 保证数据一致性和用户体验

### 3. 语言切换支持

**动态标签更新**：
```csharp
LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(PostSpeechRecordingDurationHint));
```

## 用户体验改进

### 1. 直观操作

- **滑块拖拽**：用户可以直观地调节参数
- **精确输入**：文本框支持精确数值输入
- **实时反馈**：参数变化立即生效

### 2. 清晰指导

- **范围提示**：每个参数都有明确的范围说明
- **推荐值**：提供最佳实践建议
- **功能说明**：解释每个参数的作用

### 3. 防错设计

- **自动限制**：超出范围自动校正
- **视觉反馈**：滑块轨道显示有效范围
- **一致性**：所有语音参数使用统一的界面风格

## 参数关系说明

### 实际最小录音时长

由于追加录音逻辑，实际的最小录音时长为：
```
实际最小时长 = 最小语音时长 + 追加录音时长
```

例如：
- 最小语音时长：0.5 秒
- 追加录音时长：0.5 秒
- 实际最小时长：1.0 秒

### 参数建议

**快速响应场景**：
- 追加录音时长：0.3 秒
- 最小语音时长：0.3 秒
- 最大语音时长：5 秒

**平衡场景（推荐）**：
- 追加录音时长：0.5 秒
- 最小语音时长：0.5 秒
- 最大语音时长：10 秒

**保守场景**：
- 追加录音时长：0.7 秒
- 最小语音时长：0.7 秒
- 最大语音时长：10 秒

## 构建状态

✅ 构建成功，只有一个可忽略的 null 引用警告
✅ 所有重复资源警告已修复
✅ UI 界面完全可用
