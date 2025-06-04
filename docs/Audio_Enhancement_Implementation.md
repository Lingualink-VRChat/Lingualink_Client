# 音频增强功能实施文档

## 概述

本文档描述了在 Lingualink Client 中实施的音频增强功能，该功能通过 RMS 检测的条件增益和峰值归一化来提高语音识别的准确性。

## 功能特性

### 1. 安静语音增强 (Quiet Boost)
- **RMS 检测**: 计算音频片段的 RMS (Root Mean Square) 值
- **条件增益**: 只对 RMS 低于设定阈值的片段应用增益
- **智能放大**: 优先处理音量偏低的语音片段，提高信噪比

### 2. 峰值归一化 (Peak Normalization)
- **电平控制**: 将音频峰值调整到统一的目标电平
- **防止削波**: 确保音频不会超出数字音频的动态范围
- **一致性输出**: 为后端 LLM 提供电平一致的音频输入

## 技术实现

### 核心处理流程

1. **音频捕获**: AudioService 捕获音频片段
2. **RMS 计算**: 计算整个片段的 RMS 值并转换为 dBFS
3. **条件增益**: 如果 RMS < 阈值，应用指定的增益
4. **峰值归一化**: 将音频峰值调整到目标电平
5. **限幅处理**: 防止样本值超出 16-bit 范围

### 关键算法

#### RMS 计算
```csharp
double sumOfSquares = 0.0;
for (int i = 0; i < samples.Length; i++)
{
    double sample = samples[i];
    sumOfSquares += sample * sample;
}
double rms = Math.Sqrt(sumOfSquares / samples.Length);
double rmsDbFs = 20.0 * Math.Log10(rms / short.MaxValue);
```

#### 条件增益应用
```csharp
if (rmsDbFs < _appSettings.QuietBoostRmsThresholdDbFs)
{
    double gainFactor = Math.Pow(10.0, _appSettings.QuietBoostGainDb / 20.0);
    // 应用增益并限幅
    for (int i = 0; i < samples.Length; i++)
    {
        double amplifiedSample = samples[i] * gainFactor;
        samples[i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, amplifiedSample));
    }
}
```

#### 峰值归一化
```csharp
double targetAmplitudeLinear = Math.Pow(10.0, _appSettings.NormalizationTargetDb / 20.0) * short.MaxValue;
double scaleFactor = targetAmplitudeLinear / maxAbsSample;
// 应用缩放并限幅
```

## 配置参数

### 音频增强设置
- **EnableAudioNormalization**: 是否启用峰值归一化 (默认: true)
- **NormalizationTargetDb**: 峰值归一化目标电平 (默认: -3.0 dBFS)
- **EnableQuietBoost**: 是否启用安静语音增强 (默认: true)
- **QuietBoostRmsThresholdDbFs**: RMS 阈值 (默认: -25.0 dBFS)
- **QuietBoostGainDb**: 安静片段增益 (默认: 6.0 dB)

### 参数范围
- **NormalizationTargetDb**: -20dB 到 0dB
- **QuietBoostRmsThresholdDbFs**: -60dB 到 0dB
- **QuietBoostGainDb**: 0dB 到 20dB

## 文件修改清单

### 1. Models/AppSettings.cs
- 添加了 5 个新的音频增强配置属性

### 2. Services/AudioService.cs
- 修改构造函数接受 ILoggingManager 参数
- 添加 ProcessAndNormalizeAudio 方法
- 添加 ApplyQuietBoost 方法
- 添加 ApplyPeakNormalization 方法
- 在音频分割点调用音频处理方法

### 3. Services/Managers/AudioTranslationOrchestrator.cs
- 更新 AudioService 实例化以传递 ILoggingManager

### 4. ViewModels/ServicePageViewModel.cs
- 添加 5 个新的 ObservableProperty
- 添加属性验证回调
- 更新 LoadSettingsFromModel 和 ValidateAndBuildSettings 方法
- 添加本地化标签属性
- 添加语言变化事件处理

### 5. Views/Pages/ServicePage.xaml
- 添加新的音频增强设置 CardExpander
- 包含峰值归一化和安静语音增强的 UI 控件

### 6. Properties/Lang.resx 和 Lang.zh-CN.resx
- 添加音频增强相关的本地化字符串

## 使用效果

### 对 LLM 后端的改进
1. **提高信噪比**: 安静语音被放大后在背景噪声中更突出
2. **一致的电平**: 峰值归一化确保所有音频片段具有相似的电平
3. **减少识别错误**: 更清晰的音频输入提高语音识别准确率
4. **智能处理**: 只对需要的片段应用增益，避免放大噪声

### 注意事项
1. **噪声放大**: 安静语音增强可能同时放大背景噪声
2. **参数调整**: 需要根据实际使用环境调整阈值和增益参数
3. **性能影响**: 增加了少量 CPU 开销用于音频处理
4. **日志记录**: 所有音频处理操作都会记录到日志中

## 测试建议

1. **不同音量测试**: 测试正常音量和安静语音的处理效果
2. **噪声环境测试**: 在有背景噪声的环境中测试
3. **参数调优**: 根据实际使用情况调整各项参数
4. **性能测试**: 监控音频处理对系统性能的影响
