// lingualink_client.Models.Models.cs
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using lingualink_client.Services;

namespace lingualink_client.Models
{
    // MMDeviceWrapper remains the same

    public class MMDeviceWrapper
    {
        public string ID { get; }
        public string FriendlyName { get; }
        public int WaveInDeviceIndex { get; set; }

        public MMDeviceWrapper(MMDevice device, int waveInIndex)
        {
            ID = device.ID;
            FriendlyName = device.FriendlyName;
            WaveInDeviceIndex = waveInIndex;
        }

        public override string ToString() => FriendlyName;
    }

    // Legacy ServerResponse for backward compatibility
    public class ServerResponse
    {
        public string? Status { get; set; }
        public double Duration_Seconds { get; set; }
        [JsonConverter(typeof(TranslationDataConverter))]
        public TranslationData? Data { get; set; } // This will hold our simplified TranslationData
        public string? Message { get; set; }
        public ErrorDetails? Details { get; set; }
    }

    // New API Response Models (v2.0)
    public class NewApiResponse
    {
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("transcription")]
        public string? Transcription { get; set; }

        [JsonPropertyName("translations")]
        public Dictionary<string, string>? Translations { get; set; }

        [JsonPropertyName("raw_response")]
        public string? RawResponse { get; set; }

        [JsonPropertyName("processing_time")]
        public double ProcessingTime { get; set; }

        [JsonPropertyName("metadata")]
        public ApiMetadata? Metadata { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public class ApiMetadata
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("backend")]
        public string? Backend { get; set; }

        [JsonPropertyName("original_format")]
        public string? OriginalFormat { get; set; }

        [JsonPropertyName("processed_format")]
        public string? ProcessedFormat { get; set; }

        [JsonPropertyName("conversion_applied")]
        public bool ConversionApplied { get; set; }
    }

    public class TranslationData // Enhanced to support dynamic language fields
    {
        // Primary field we are interested in now
        public string? Raw_Text { get; set; }

        // Dynamic language fields - will be populated from JSON
        private Dictionary<string, string> _languageFields = new Dictionary<string, string>();

        // Predefined common language fields for backward compatibility
        public string? 原文 { get; set; }
        public string? 英文 { get; set; }
        public string? 日文 { get; set; }
        public string? 中文 { get; set; }
        public string? 韩文 { get; set; }
        public string? 法文 { get; set; }
        public string? 德文 { get; set; }
        public string? 西班牙文 { get; set; }
        public string? 俄文 { get; set; }
        public string? 意大利文 { get; set; }

        // Legacy fields for backward compatibility
        public string? Original_Language { get; set; }
        public string? Original_Text { get; set; }
        public string? English_Translation { get; set; }
        public string? Japanese_Translation { get; set; }

        // Method to get all available language fields
        public Dictionary<string, string> GetAllLanguageFields()
        {
            var fields = new Dictionary<string, string>();
            
            // Add predefined fields if they have values
            if (!string.IsNullOrEmpty(原文)) fields["原文"] = 原文;
            if (!string.IsNullOrEmpty(英文)) fields["英文"] = 英文;
            if (!string.IsNullOrEmpty(日文)) fields["日文"] = 日文;
            if (!string.IsNullOrEmpty(中文)) fields["中文"] = 中文;
            if (!string.IsNullOrEmpty(韩文)) fields["韩文"] = 韩文;
            if (!string.IsNullOrEmpty(法文)) fields["法文"] = 法文;
            if (!string.IsNullOrEmpty(德文)) fields["德文"] = 德文;
            if (!string.IsNullOrEmpty(西班牙文)) fields["西班牙文"] = 西班牙文;
            if (!string.IsNullOrEmpty(俄文)) fields["俄文"] = 俄文;
            if (!string.IsNullOrEmpty(意大利文)) fields["意大利文"] = 意大利文;

            // Add any additional dynamic fields
            foreach (var kvp in _languageFields)
            {
                if (!fields.ContainsKey(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                {
                    fields[kvp.Key] = kvp.Value;
                }
            }

            return fields;
        }

        // Method to set a language field dynamically
        public void SetLanguageField(string language, string value)
        {
            _languageFields[language] = value;
        }

        // Method to get a specific language field
        public string? GetLanguageField(string language)
        {
            // Check predefined fields first
            switch (language)
            {
                case "原文": return 原文;
                case "英文": return 英文;
                case "日文": return 日文;
                case "中文": return 中文;
                case "韩文": return 韩文;
                case "法文": return 法文;
                case "德文": return 德文;
                case "西班牙文": return 西班牙文;
                case "俄文": return 俄文;
                case "意大利文": return 意大利文;
                default:
                    return _languageFields.TryGetValue(language, out var value) ? value : null;
            }
        }
    }

    // Custom JSON converter for TranslationData
    public class TranslationDataConverter : JsonConverter<TranslationData>
    {
        public override TranslationData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var data = new TranslationData();
            
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString()!;
                    reader.Read();
                    string? value = reader.GetString();

                    // Handle known properties
                    switch (propertyName.ToLowerInvariant())
                    {
                        case "raw_text":
                            data.Raw_Text = value;
                            break;
                        case "original_language":
                            data.Original_Language = value;
                            break;
                        case "original_text":
                            data.Original_Text = value;
                            break;
                        case "english_translation":
                            data.English_Translation = value;
                            break;
                        case "japanese_translation":
                            data.Japanese_Translation = value;
                            break;
                        default:
                            // Handle language fields
                            switch (propertyName)
                            {
                                case "原文":
                                    data.原文 = value;
                                    break;
                                case "英文":
                                    data.英文 = value;
                                    break;
                                case "日文":
                                    data.日文 = value;
                                    break;
                                case "中文":
                                    data.中文 = value;
                                    break;
                                case "韩文":
                                    data.韩文 = value;
                                    break;
                                case "法文":
                                    data.法文 = value;
                                    break;
                                case "德文":
                                    data.德文 = value;
                                    break;
                                case "西班牙文":
                                    data.西班牙文 = value;
                                    break;
                                case "俄文":
                                    data.俄文 = value;
                                    break;
                                case "意大利文":
                                    data.意大利文 = value;
                                    break;
                                default:
                                    // Store any other language fields dynamically
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        data.SetLanguageField(propertyName, value);
                                    }
                                    break;
                            }
                            break;
                    }
                }
            }

            return data;
        }

        public override void Write(Utf8JsonWriter writer, TranslationData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            
            if (value.Raw_Text != null)
                writer.WriteString("raw_text", value.Raw_Text);
            
            if (value.原文 != null)
                writer.WriteString("原文", value.原文);
            
            if (value.英文 != null)
                writer.WriteString("英文", value.英文);
            
            if (value.日文 != null)
                writer.WriteString("日文", value.日文);
            
            if (value.中文 != null)
                writer.WriteString("中文", value.中文);
            
            if (value.韩文 != null)
                writer.WriteString("韩文", value.韩文);
            
            if (value.法文 != null)
                writer.WriteString("法文", value.法文);
            
            if (value.德文 != null)
                writer.WriteString("德文", value.德文);
            
            if (value.西班牙文 != null)
                writer.WriteString("西班牙文", value.西班牙文);
            
            if (value.俄文 != null)
                writer.WriteString("俄文", value.俄文);
            
            if (value.意大利文 != null)
                writer.WriteString("意大利文", value.意大利文);

            // Write legacy fields
            if (value.Original_Language != null)
                writer.WriteString("original_language", value.Original_Language);
            
            if (value.Original_Text != null)
                writer.WriteString("original_text", value.Original_Text);
            
            if (value.English_Translation != null)
                writer.WriteString("english_translation", value.English_Translation);
            
            if (value.Japanese_Translation != null)
                writer.WriteString("japanese_translation", value.Japanese_Translation);

            writer.WriteEndObject();
        }
    }

    public class ErrorDetails // Remains the same
    {
        public int Status_Code { get; set; }
        public JsonElement? Content { get; set; }
    }

    public class AudioSegmentEventArgs : EventArgs // Remains the same
    {
        public byte[] AudioData { get; }
        public string TriggerReason { get; }

        public AudioSegmentEventArgs(byte[] audioData, string triggerReason)
        {
            AudioData = audioData;
            TriggerReason = triggerReason;
        }
    }

    // Template processing related classes
    public class MessageTemplate
    {
        public string Name { get; set; } = "";
        public string Template { get; set; } = "";
        public string Description { get; set; } = "";

        public MessageTemplate() { }

        public MessageTemplate(string name, string template, string description = "")
        {
            Name = name;
            Template = template;
            Description = description;
        }
    }

    public static class TemplateProcessor
    {
        public static string ProcessTemplate(string template, TranslationData data)
        {
            if (string.IsNullOrEmpty(template) || data == null)
                return template ?? "";

            string result = template;

            // Replace {raw_text} or {原始文本}
            if (!string.IsNullOrEmpty(data.Raw_Text))
            {
                result = result.Replace("{raw_text}", data.Raw_Text);
                result = result.Replace("{原始文本}", data.Raw_Text);
                result = result.Replace("{完整文本}", data.Raw_Text);
            }

            // Replace language-specific placeholders
            var languageFields = data.GetAllLanguageFields();
            foreach (var kvp in languageFields)
            {
                string placeholder = $"{{{kvp.Key}}}";
                result = result.Replace(placeholder, kvp.Value);
            }

            // Handle some common alternative placeholder formats
            result = result.Replace("{原文本}", data.GetLanguageField("原文") ?? "");
            result = result.Replace("{英语}", data.GetLanguageField("英文") ?? "");
            result = result.Replace("{日语}", data.GetLanguageField("日文") ?? "");

            return result;
        }

        /// <summary>
        /// Check if the processed template still contains unreplaced placeholders.
        /// Supports both new format ({en}, {ja}) and legacy format ({英文}, {日文}).
        /// </summary>
        /// <param name="processedText">Text after template processing</param>
        /// <returns>True if there are still unreplaced placeholders, false otherwise</returns>
        public static bool ContainsUnreplacedPlaceholders(string processedText)
        {
            if (string.IsNullOrEmpty(processedText))
                return false;

            // Check for language code placeholders like {en}, {ja}
            var codeMatches = System.Text.RegularExpressions.Regex.Matches(processedText, @"\{([a-z]{2,3}(?:-[A-Za-z0-9]+)?)\}");
            foreach (System.Text.RegularExpressions.Match match in codeMatches)
            {
                var code = match.Groups[1].Value;
                if (LanguageDisplayHelper.IsLanguageCodeSupported(code))
                {
                    return true; // Found unreplaced language code placeholder
                }
            }

            // Check for legacy Chinese name placeholders
            var availableChineseNames = new[] { "原文", "英文", "日文", "中文", "韩文", "法文", "德文", "西班牙文", "俄文", "意大利文" };
            foreach (var lang in availableChineseNames)
            {
                if (processedText.Contains($"{{{lang}}}"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Process template and return null if the result contains unreplaced placeholders
        /// </summary>
        /// <param name="template">Template string</param>
        /// <param name="data">Translation data</param>
        /// <returns>Processed text or null if unreplaced placeholders remain</returns>
        public static string? ProcessTemplateWithValidation(string template, TranslationData data)
        {
            var processedText = ProcessTemplate(template, data);
            
            if (ContainsUnreplacedPlaceholders(processedText))
            {
                return null; // Indicate that this should not be sent
            }
            
            return processedText;
        }

        public static List<PlaceholderItem> GetAvailablePlaceholders(TranslationData? sampleData = null)
        {
            var placeholders = new List<PlaceholderItem>();

            // 添加一个明确且本地化的原文占位符
            placeholders.Add(new PlaceholderItem
            {
                DisplayName = LanguageManager.GetString("Lang_SourceText"),
                Value = "{transcription}"
            });

            // Generate universal, user-friendly placeholder format: "Display Name ({code})"
            // This format is intuitive for all users regardless of UI language
            foreach (var backendName in LanguageDisplayHelper.BackendLanguageNames)
            {
                // 跳过"仅转录"选项，因为我们已经在上面单独添加了
                if (backendName == LanguageDisplayHelper.TranscriptionBackendName)
                    continue;

                var displayName = LanguageDisplayHelper.GetDisplayName(backendName); // "English", "Japanese", etc.
                var code = LanguageDisplayHelper.ConvertChineseNameToLanguageCode(backendName); // "en", "ja", etc.
                if (!string.IsNullOrEmpty(code))
                {
                    placeholders.Add(new PlaceholderItem
                    {
                        DisplayName = $"{displayName} ({code})",
                        Value = $"{{{code}}}"
                    });
                }
            }

            if (sampleData != null)
            {
                var languageFields = sampleData.GetAllLanguageFields();
                foreach (var language in languageFields.Keys)
                {
                    string placeholder = $"{{{language}}}";
                    if (!placeholders.Any(p => p.Value.Contains(placeholder)))
                    {
                        placeholders.Add(new PlaceholderItem
                        {
                            DisplayName = placeholder,
                            Value = placeholder
                        });
                    }
                }
            }

            return placeholders;
        }

        /// <summary>
        /// Create sample data for template preview with multiple languages
        /// </summary>
        public static TranslationData CreateSamplePreviewData()
        {
            return new TranslationData
            {
                Raw_Text = "原文：你好世界\n英文：Hello World\n日文：こんにちは世界\n法文：Bonjour le monde\n德文：Hallo Welt\n西班牙文：Hola Mundo\n韩文：안녕하세요 세계\n俄文：Привет мир",
                原文 = "你好世界",
                英文 = "Hello World",
                日文 = "こんにちは世界",
                法文 = "Bonjour le monde",
                德文 = "Hallo Welt",
                西班牙文 = "Hola Mundo",
                韩文 = "안녕하세요 세계",
                俄文 = "Привет мир",
                中文 = "你好世界",
                意大利文 = "Ciao mondo"
            };
        }

        /// <summary>
        /// 从模板字符串中提取唯一的语言代码。
        /// </summary>
        /// <param name="template">要分析的模板字符串</param>
        /// <param name="limit">要提取的最大语言数量。如果为0或负数，则不限制。</param>
        /// <returns>在模板中找到的唯一语言代码列表（例如 "en", "ja"）。</returns>
        public static List<string> ExtractLanguagesFromTemplate(string template, int limit = 0)
        {
            if (string.IsNullOrEmpty(template))
                return new List<string>();

            var languageCodes = new HashSet<string>();

            // 正则表达式，用于查找 {en}, {ja}, {zh-hant} 等占位符
            var codeMatches = System.Text.RegularExpressions.Regex.Matches(template, @"\{([a-z]{2,3}(?:-[A-Za-z0-9]+)?)\}");
            foreach (System.Text.RegularExpressions.Match match in codeMatches)
            {
                var code = match.Groups[1].Value;
                if (LanguageDisplayHelper.IsLanguageCodeSupported(code))
                {
                    languageCodes.Add(code);
                    if (limit > 0 && languageCodes.Count >= limit)
                        break;
                }
            }

            // 如果没有找到语言代码，检查传统格式: {英文}, {日文}
            if (languageCodes.Count == 0)
            {
                var availableChineseNames = new[] { "英文", "日文", "中文", "韩文", "法文", "德文", "西班牙文", "俄文", "意大利文" };
                foreach (var chineseName in availableChineseNames)
                {
                    if (template.Contains($"{{{chineseName}}}"))
                    {
                        var code = LanguageDisplayHelper.ConvertChineseNameToLanguageCode(chineseName);
                        if (!string.IsNullOrEmpty(code))
                        {
                            languageCodes.Add(code);
                            if (limit > 0 && languageCodes.Count >= limit)
                                break;
                        }
                    }
                }
            }

            return languageCodes.ToList();
        }
    }

    /// <summary>
    /// Represents a placeholder item for the UI, separating display text from insertion value.
    /// </summary>
    public class PlaceholderItem
    {
        /// <summary>
        /// The text displayed on the button (e.g., "Source Text", "English (en)").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>
        /// The value inserted into the textbox (e.g., "{transcription}", "{en}").
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}