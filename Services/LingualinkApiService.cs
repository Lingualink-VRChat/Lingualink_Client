using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace lingualink_client.Services
{
    /// <summary>
    /// Lingualink Core API v2.0 服务实现
    /// 专注于新API，移除旧版兼容逻辑
    /// </summary>
    public class LingualinkApiService : ILingualinkApiService
    {
        private readonly string _serverUrl;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly AudioEncoderService? _audioEncoder;
        private readonly bool _useOpusEncoding;
        private bool _disposed = false;

        public LingualinkApiService(
            string serverUrl,
            string apiKey = "",
            int opusComplexity = 7)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _apiKey = apiKey;
            _useOpusEncoding = true; // Opus始终启用

            Debug.WriteLine($"[LingualinkApiService] Constructor called - ServerUrl: '{_serverUrl}', ApiKey: '{_apiKey}', UseOpus: true (always enabled)");

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            // 设置认证头
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", _apiKey);
                Debug.WriteLine($"[LingualinkApiService] Added X-API-Key header: '{_apiKey}'");
            }
            else
            {
                Debug.WriteLine($"[LingualinkApiService] No API key provided, skipping header");
            }

            // 初始化音频编码器 - Opus始终启用，固定16kbps比特率
            try
            {
                // 使用正确的参数顺序：sampleRate, channels, bitrate, complexity
                // 从AudioService获取应用程序的音频配置
                int appSampleRate = AudioService.APP_SAMPLE_RATE; // 16000
                int appChannels = AudioService.APP_CHANNELS;       // 1
                const int fixedBitrate = 16000; // 固定16kbps比特率以获得更高压缩率

                _audioEncoder = new AudioEncoderService(
                    sampleRate: appSampleRate,    // 16000 Hz
                    channels: appChannels,        // 1 channel (mono)
                    bitrate: fixedBitrate,        // 固定16kbps比特率
                    complexity: opusComplexity    // 来自设置的复杂度 (5-10)
                );
                Debug.WriteLine($"[LingualinkApiService] Opus encoder initialized: SR={appSampleRate}, CH={appChannels}, Bitrate={fixedBitrate}, Complexity={opusComplexity}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Failed to initialize Opus encoder: {ex.Message}. Opus encoding will be disabled.");
                _audioEncoder = null; // 确保初始化失败时为null
            }
        }

        public async Task<ApiResult> ProcessAudioAsync(
            byte[] audioData,
            WaveFormat waveFormat,
            IEnumerable<string> targetLanguages,
            string task = "translate",
            string triggerReason = "manual",
            CancellationToken cancellationToken = default)
        {
            if (audioData.Length == 0)
            {
                return new ApiResult 
                { 
                    IsSuccess = false, 
                    ErrorMessage = LanguageManager.GetString("ErrorAudioEmpty") 
                };
            }

            var targetLangArray = targetLanguages.ToArray();

            cancellationToken.ThrowIfCancellationRequested();

            // [核心修复] 只有在任务是"翻译"时，才要求目标语言。
            if (task == "translate" && targetLangArray.Length == 0)
            {
                // 如果是翻译任务，但没有目标语言，则返回错误。
                Debug.WriteLine($"[LingualinkApiService] Error: 'translate' task requires target languages, but none were provided.");
                return new ApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Target languages are required for the translation task."
                };
            }

            var requestUrl = new Uri(_serverUrl.TrimEnd('/') + "/process_audio");
            Debug.WriteLine($"[LingualinkApiService] Processing audio: {requestUrl}, task: {task}, targets: [{string.Join(", ", targetLangArray)}]");

            try
            {
                // 准备音频数据
                string audioBase64;
                string audioFormat;

                if (_useOpusEncoding && _audioEncoder != null)
                {
                    try
                    {
                        var opusData = _audioEncoder.EncodePcmToOpus(audioData, waveFormat);
                        audioBase64 = Convert.ToBase64String(opusData);
                        audioFormat = "opus";
                        
                        var compressionRatio = AudioEncoderService.CalculateCompressionRatio(audioData.Length, opusData.Length);
                        Debug.WriteLine($"[LingualinkApiService] Opus encoding: {audioData.Length} -> {opusData.Length} bytes (compression: {compressionRatio:F1}%)");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LingualinkApiService] Opus encoding failed: {ex.Message}, falling back to WAV");
                        audioBase64 = Convert.ToBase64String(audioData);
                        audioFormat = "wav";
                    }
                }
                else
                {
                    audioBase64 = Convert.ToBase64String(audioData);
                    audioFormat = "wav";
                }

                // 创建请求负载
                var requestPayload = new
                {
                    audio = audioBase64,
                    audio_format = audioFormat,
                    task = task, // 使用传入的task参数
                    target_languages = targetLangArray
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // [详细日志] 记录请求JSON内容
                Debug.WriteLine($"[LingualinkApiService] Request JSON: {jsonContent}");

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // [详细日志] 记录响应状态和完整JSON内容
                Debug.WriteLine($"[LingualinkApiService] Response Status: {response.StatusCode} ({(int)response.StatusCode})");
                Debug.WriteLine($"[LingualinkApiService] Response JSON: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<NewApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse != null)
                    {
                        // [详细日志] 记录解析后的API响应详情
                        var isSuccessful = apiResponse.Status == "success" || apiResponse.Status == "partial_success";
                        Debug.WriteLine($"[LingualinkApiService] Parsed API Response:");
                        Debug.WriteLine($"  - Status: {apiResponse.Status} (IsSuccess: {isSuccessful})");
                        Debug.WriteLine($"  - RequestId: {apiResponse.RequestId}");
                        Debug.WriteLine($"  - Transcription: {apiResponse.Transcription}");
                        Debug.WriteLine($"  - Translations Count: {apiResponse.Translations?.Count ?? 0}");
                        if (apiResponse.Translations != null)
                        {
                            foreach (var translation in apiResponse.Translations)
                            {
                                Debug.WriteLine($"    - {translation.Key}: {translation.Value}");
                            }
                        }
                        Debug.WriteLine($"  - ProcessingTime: {apiResponse.ProcessingTime}ms");
                        Debug.WriteLine($"  - Error: {apiResponse.Error}");

                        return new ApiResult
                        {
                            IsSuccess = apiResponse.Status == "success" || apiResponse.Status == "partial_success",
                            RequestId = apiResponse.RequestId,
                            Transcription = apiResponse.Transcription,
                            Translations = apiResponse.Translations ?? new Dictionary<string, string>(),
                            RawResponse = apiResponse.RawResponse,
                            ProcessingTime = apiResponse.ProcessingTime,
                            Metadata = apiResponse.Metadata,
                            ErrorMessage = apiResponse.Error
                        };
                    }
                    else
                    {
                        Debug.WriteLine($"[LingualinkApiService] Failed to deserialize API response - apiResponse is null");
                    }
                }

                // 处理错误响应
                return await HandleErrorResponse(response, responseContent);
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Request timeout: {ex.Message}");
                Debug.WriteLine($"[LingualinkApiService] Stack trace: {ex.StackTrace}");
                return new ApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Request timeout: {ex.Message}"
                };
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Network error: {ex.Message}");
                Debug.WriteLine($"[LingualinkApiService] Stack trace: {ex.StackTrace}");
                return new ApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Network error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Unexpected error: {ex.Message}");
                Debug.WriteLine($"[LingualinkApiService] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[LingualinkApiService] Stack trace: {ex.StackTrace}");
                return new ApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<ApiResult> ProcessTextAsync(
            string text,
            IEnumerable<string> targetLanguages,
            string? sourceLanguage = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ApiResult 
                { 
                    IsSuccess = false, 
                    ErrorMessage = "Text is required" 
                };
            }

            var targetLangArray = targetLanguages.ToArray();

            cancellationToken.ThrowIfCancellationRequested();
            if (targetLangArray.Length == 0)
            {
                return new ApiResult 
                { 
                    IsSuccess = false, 
                    ErrorMessage = "Target languages are required" 
                };
            }

            var requestUrl = new Uri(_serverUrl.TrimEnd('/') + "/process_text");
            Debug.WriteLine($"[LingualinkApiService] Processing text: {requestUrl}, targets: [{string.Join(", ", targetLangArray)}]");

            try
            {
                // 创建请求负载
                var requestPayload = new
                {
                    text = text,
                    target_languages = targetLangArray,
                    source_language = sourceLanguage
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                // [详细日志] 记录请求JSON内容
                Debug.WriteLine($"[LingualinkApiService] Text Request JSON: {jsonContent}");

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // [详细日志] 记录响应状态和完整JSON内容
                Debug.WriteLine($"[LingualinkApiService] Text Response Status: {response.StatusCode} ({(int)response.StatusCode})");
                Debug.WriteLine($"[LingualinkApiService] Text Response JSON: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<TextApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResponse != null)
                    {
                        // [详细日志] 记录解析后的文本API响应详情
                        var isSuccessful = apiResponse.Status == "success" || apiResponse.Status == "partial_success";
                        Debug.WriteLine($"[LingualinkApiService] Parsed Text API Response:");
                        Debug.WriteLine($"  - Status: {apiResponse.Status} (IsSuccess: {isSuccessful})");
                        Debug.WriteLine($"  - RequestId: {apiResponse.RequestId}");
                        Debug.WriteLine($"  - SourceText: {apiResponse.SourceText}");
                        Debug.WriteLine($"  - Translations Count: {apiResponse.Translations?.Count ?? 0}");
                        if (apiResponse.Translations != null)
                        {
                            foreach (var translation in apiResponse.Translations)
                            {
                                Debug.WriteLine($"    - {translation.Key}: {translation.Value}");
                            }
                        }
                        Debug.WriteLine($"  - ProcessingTime: {apiResponse.ProcessingTime}ms");
                        Debug.WriteLine($"  - Error: {apiResponse.Error}");

                        return new ApiResult
                        {
                            IsSuccess = apiResponse.Status == "success" || apiResponse.Status == "partial_success",
                            RequestId = apiResponse.RequestId,
                            Transcription = apiResponse.SourceText, // 对于文本处理，源文本作为"转录"
                            Translations = apiResponse.Translations ?? new Dictionary<string, string>(),
                            RawResponse = apiResponse.RawResponse,
                            ProcessingTime = apiResponse.ProcessingTime,
                            Metadata = apiResponse.Metadata,
                            ErrorMessage = apiResponse.Error
                        };
                    }
                    else
                    {
                        Debug.WriteLine($"[LingualinkApiService] Failed to deserialize Text API response - apiResponse is null");
                    }
                }

                // 处理错误响应
                return await HandleErrorResponse(response, responseContent);
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Text request timeout: {ex.Message}");
                Debug.WriteLine($"[LingualinkApiService] Stack trace: {ex.StackTrace}");
                return new ApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Request timeout: {ex.Message}"
                };
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Text network error: {ex.Message}");
                Debug.WriteLine($"[LingualinkApiService] Stack trace: {ex.StackTrace}");
                return new ApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Network error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Text unexpected error: {ex.Message}");
                Debug.WriteLine($"[LingualinkApiService] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[LingualinkApiService] Stack trace: {ex.StackTrace}");
                return new ApiResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}"
                };
            }
        }

        public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = new Uri(_serverUrl.TrimEnd('/') + "/health");
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var healthResponse = JsonSerializer.Deserialize<HealthResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return healthResponse?.Status == "healthy";
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Connection validation failed: {ex.Message}");
                return false;
            }
        }

        public async Task<SystemCapabilities?> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = new Uri(_serverUrl.TrimEnd('/') + "/capabilities");
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    return JsonSerializer.Deserialize<SystemCapabilities>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Failed to get capabilities: {ex.Message}");
                return null;
            }
        }

        public async Task<LanguageInfo[]?> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = new Uri(_serverUrl.TrimEnd('/') + "/languages");
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var languageResponse = JsonSerializer.Deserialize<LanguageListResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return languageResponse?.Languages;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LingualinkApiService] Failed to get supported languages: {ex.Message}");
                return null;
            }
        }

        private Task<ApiResult> HandleErrorResponse(HttpResponseMessage response, string responseContent)
        {
            // [详细日志] 记录错误响应的详细信息
            Debug.WriteLine($"[LingualinkApiService] Error Response Details:");
            Debug.WriteLine($"  - Status Code: {response.StatusCode} ({(int)response.StatusCode})");
            Debug.WriteLine($"  - Response Content: {responseContent}");

            try
            {
                // 尝试解析错误响应
                var errorResponse = JsonSerializer.Deserialize<NewApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Error))
                {
                    Debug.WriteLine($"  - Parsed Error: {errorResponse.Error}");
                    return Task.FromResult(new ApiResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Server error ({(int)response.StatusCode}): {errorResponse.Error}"
                    });
                }
            }
            catch (Exception ex)
            {
                // 记录JSON解析错误
                Debug.WriteLine($"  - JSON Parse Error: {ex.Message}");
            }

            // 处理特殊HTTP状态码
            var errorMessage = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Authentication failed. Please check your API key.",
                System.Net.HttpStatusCode.Forbidden => "Access forbidden. Insufficient permissions.",
                System.Net.HttpStatusCode.NotFound => "API endpoint not found. Please check the server URL.",
                System.Net.HttpStatusCode.RequestEntityTooLarge => "Request too large. Please reduce audio file size or text length.",
                System.Net.HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait before making more requests.",
                _ => $"Server error ({(int)response.StatusCode}): {responseContent}"
            };

            return Task.FromResult(new ApiResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                    _audioEncoder?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    // 响应模型类
    internal class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public string Version { get; set; } = string.Empty;
    }

    internal class LanguageListResponse
    {
        public LanguageInfo[] Languages { get; set; } = Array.Empty<LanguageInfo>();
        public int Count { get; set; }
    }

    internal class TextApiResponse
    {
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("source_text")]
        public string? SourceText { get; set; }

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
}

