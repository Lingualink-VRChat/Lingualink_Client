﻿// lingualink_client.Models.Models.cs
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public class ServerResponse
    {
        public string? Status { get; set; }
        public double Duration_Seconds { get; set; }
        [JsonConverter(typeof(TranslationDataConverter))]
        public TranslationData? Data { get; set; } // This will hold our simplified TranslationData
        public string? Message { get; set; }
        public ErrorDetails? Details { get; set; }
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
        public string? Content { get; set; }
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
        public bool IsDefault { get; set; } = false;

        public MessageTemplate() { }

        public MessageTemplate(string name, string template, string description = "", bool isDefault = false)
        {
            Name = name;
            Template = template;
            Description = description;
            IsDefault = isDefault;
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

        public static List<string> GetAvailablePlaceholders(TranslationData? sampleData = null)
        {
            var placeholders = new List<string>();
            
            // Add localized placeholders based on current UI language
            var currentLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            
            if (currentLanguage.StartsWith("zh")) // Chinese
            {
                placeholders.AddRange(new[]
                {
                    "{原文}",
                    "{英文}",
                    "{日文}",
                    "{中文}",
                    "{韩文}",
                    "{法文}",
                    "{德文}",
                    "{西班牙文}",
                    "{俄文}"
                });
            }
            else // English and other languages
            {
                placeholders.AddRange(new[]
                {
                    "{原文}",
                    "{英文}",
                    "{日文}",
                    "{中文}",
                    "{韩文}",
                    "{法文}",
                    "{德文}",
                    "{西班牙文}",
                    "{俄文}"
                });
            }

            if (sampleData != null)
            {
                var languageFields = sampleData.GetAllLanguageFields();
                foreach (var language in languageFields.Keys)
                {
                    string placeholder = $"{{{language}}}";
                    if (!placeholders.Contains(placeholder))
                    {
                        placeholders.Add(placeholder);
                    }
                }
            }

            return placeholders;
        }

        public static List<MessageTemplate> GetDefaultTemplates()
        {
            var currentLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            
            if (currentLanguage.StartsWith("zh")) // Chinese
            {
                return new List<MessageTemplate>
                {
                    new MessageTemplate("完整文本", "{raw_text}", "显示服务器返回的完整原始文本", true),
                    new MessageTemplate("仅原文", "{原文}", "只显示原文内容"),
                    new MessageTemplate("原文+英文", "{原文}\n{英文}", "显示原文和英文翻译"),
                    new MessageTemplate("原文+日文", "{原文}\n{日文}", "显示原文和日文翻译"),
                    new MessageTemplate("三语对照", "{原文}\n{英文}\n{日文}", "显示原文、英文和日文"),
                    new MessageTemplate("英文优先", "{英文}", "只显示英文翻译"),
                    new MessageTemplate("日文优先", "{日文}", "只显示日文翻译"),
                    new MessageTemplate("自定义格式", "原文: {原文}\n英文: {英文}\n日文: {日文}", "带标签的格式化显示")
                };
            }
            else // English and other languages
            {
                return new List<MessageTemplate>
                {
                    new MessageTemplate("Full Text", "{raw_text}", "Show complete server response", true),
                    new MessageTemplate("Original Only", "{原文}", "Show original text only"),
                    new MessageTemplate("Original + English", "{原文}\n{英文}", "Show original and English translation"),
                    new MessageTemplate("Original + Japanese", "{原文}\n{日文}", "Show original and Japanese translation"),
                    new MessageTemplate("Three Languages", "{原文}\n{英文}\n{日文}", "Show original, English and Japanese"),
                    new MessageTemplate("English Only", "{英文}", "Show English translation only"),
                    new MessageTemplate("Japanese Only", "{日文}", "Show Japanese translation only"),
                    new MessageTemplate("Custom Format", "Original: {原文}\nEnglish: {英文}\nJapanese: {日文}", "Formatted display with labels")
                };
            }
        }
    }
}