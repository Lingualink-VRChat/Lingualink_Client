using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Text.Json;
using lingualink_client.Models;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace lingualink_client.Services
{
    public class TranslationService : IDisposable
    {
        private readonly string _serverUrl;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly AudioEncoderService? _audioEncoder;
        private readonly bool _useOpusEncoding;
        private bool _disposed = false;
 
        public TranslationService(string serverUrl, string apiKey = "", bool useOpusEncoding = true, int opusBitrate = 32000, int opusComplexity = 5)
        {
            _serverUrl = serverUrl;
            _apiKey = apiKey;
            _useOpusEncoding = useOpusEncoding;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            
            // Always attempt to set up authentication headers if an API key is provided
            if (!string.IsNullOrEmpty(_apiKey))
            {
                // Use X-API-Key header (preferred by the new backend)
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            }

            // 初始化Opus编码器（如果启用）
            if (_useOpusEncoding)
            {
                try
                {
                    _audioEncoder = new AudioEncoderService(AudioService.APP_SAMPLE_RATE, AudioService.APP_CHANNELS, opusBitrate, opusComplexity);
                    Debug.WriteLine($"[AudioEncoder] Opus encoding enabled with bitrate: {opusBitrate}bps, complexity: {opusComplexity}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AudioEncoder] Failed to initialize Opus encoder: {ex.Message}. Falling back to WAV.");
                    _audioEncoder = null;
                    _useOpusEncoding = false;
                }
            }
        }

        public async Task<bool> VerifyApiKeyAsync()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.WriteLine("[Auth] API Key is not configured. Skipping verification.");
                return false; // Or handle as an error depending on requirements
            }

            var requestUri = new Uri(new Uri(_serverUrl), "auth/verify");
            Debug.WriteLine($"[Auth] Verifying API Key at {requestUri}");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                // HttpClient already has X-API-Key header set if _apiKey is present

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("[Auth] API Key verification successful.");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[Auth] API Key verification failed. Status: {response.StatusCode}, Response: {errorContent}");
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[Auth] API Key verification request failed: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Auth] An unexpected error occurred during API Key verification: {ex.Message}");
                return false;
            }
        }

        // Modified return type
        public async Task<(ServerResponse? Response, string? RawJsonResponse, string? ErrorMessage)> TranslateAudioSegmentAsync(
            byte[] audioData,
            WaveFormat waveFormat,
            string triggerReason,
            string targetLanguagesCsv)
        {
            if (audioData.Length == 0)
            {
                return (null, null, LanguageManager.GetString("ErrorAudioEmpty"));
            }

            string tempFilePath = string.Empty;
            string? responseContentString = null; // To store raw JSON
            bool isOpusFile = false;
            int originalSize = audioData.Length;
            int encodedSize = audioData.Length;

            var requestUrl = new Uri(new Uri(_serverUrl), "translate_audio");
            Debug.WriteLine($"[TranslationService] Sending audio to: {requestUrl}");

            try
            {
                // 根据编码选择创建相应的音频文件
                if (_useOpusEncoding && _audioEncoder != null)
                {
                    tempFilePath = Path.Combine(Path.GetTempPath(), $"segment_{DateTime.Now:yyyyMMddHHmmssfff}_{triggerReason}.ogg");
                    try
                    {
                        // 编码为OGG/Opus格式
                        byte[] oggOpusData = _audioEncoder.EncodePcmToOpus(audioData, waveFormat);
                        await File.WriteAllBytesAsync(tempFilePath, oggOpusData);
                        encodedSize = oggOpusData.Length;
                        isOpusFile = true;
                        
                        // 计算压缩比率
                        double compressionRatio = AudioEncoderService.CalculateCompressionRatio(originalSize, encodedSize);
                        Debug.WriteLine($"[AudioEncoder] OGG/Opus encoding: {originalSize} bytes -> {encodedSize} bytes (compression: {compressionRatio:F1}%)");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AudioEncoder] OGG/Opus encoding failed: {ex.Message}. Falling back to WAV.");
                        // 回退到WAV格式
                        tempFilePath = Path.Combine(Path.GetTempPath(), $"segment_{DateTime.Now:yyyyMMddHHmmssfff}_{triggerReason}.wav");
                        await using (var writer = new WaveFileWriter(tempFilePath, waveFormat))
                        {
                            await writer.WriteAsync(audioData, 0, audioData.Length);
                        }
                        isOpusFile = false;
                    }
                }
                else
                {
                    // 使用传统的WAV格式
                    tempFilePath = Path.Combine(Path.GetTempPath(), $"segment_{DateTime.Now:yyyyMMddHHmmssfff}_{triggerReason}.wav");
                    await using (var writer = new WaveFileWriter(tempFilePath, waveFormat))
                    {
                        await writer.WriteAsync(audioData, 0, audioData.Length);
                    }
                    isOpusFile = false;
                }

                using (var formData = new MultipartFormDataContent())
                {
                    var fileBytes = await File.ReadAllBytesAsync(tempFilePath);
                    var fileContent = new ByteArrayContent(fileBytes);
                    
                    // 根据文件类型设置合适的Content-Type
                    if (isOpusFile)
                    {
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/ogg");
                    }
                    else
                    {
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                    }
                    
                    formData.Add(fileContent, "audio_file", Path.GetFileName(tempFilePath));

                    // Add target_languages field
                    formData.Add(new StringContent(targetLanguagesCsv), "target_languages");
                    Debug.WriteLine($"[DEBUG] TranslationService: Adding 'target_languages' = '{targetLanguagesCsv}'");

                    // Add user_prompt field as required by new API
                    formData.Add(new StringContent("请处理下面的音频。"), "user_prompt");
                    Debug.WriteLine($"[DEBUG] TranslationService: 添加 'user_prompt' = '请处理下面的音频。'");
                    
                    Debug.WriteLine($"[TranslationService] Sending HTTP POST request to {requestUrl} with audio file: {Path.GetFileName(tempFilePath)}, Content-Type: {fileContent.Headers.ContentType}, Original size: {originalSize} bytes, Encoded size: {encodedSize} bytes (Opus: {_useOpusEncoding})");

                    var httpResponse = await _httpClient.PostAsync(requestUrl, formData);
                    responseContentString = await httpResponse.Content.ReadAsStringAsync();

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        try
                        {
                            var serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseContentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            return (serverResponse, responseContentString, null); // Return raw JSON
                        }
                        catch (JsonException ex)
                        {
                            return (null, responseContentString, string.Format(LanguageManager.GetString("ErrorDeserializingSuccessResponse"), ex.Message, responseContentString));
                        }
                    }
                    else
                    {
                        // Handle authentication errors specifically
                        if (httpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            return (null, responseContentString, string.Format(LanguageManager.GetString("ErrorAuthentication"), responseContentString));
                        }
                        
                        try
                        {
                            var errorResponse = JsonSerializer.Deserialize<ServerResponse>(responseContentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                            {
                                return (errorResponse, responseContentString, string.Format(LanguageManager.GetString("ErrorServer"), (int)httpResponse.StatusCode, errorResponse.Message));
                            }
                        }
                        catch { }
                        return (null, responseContentString, string.Format(LanguageManager.GetString("ErrorServer"), (int)httpResponse.StatusCode, responseContentString));
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                return (null, responseContentString, string.Format(LanguageManager.GetString("ErrorNetworkTimeout"), ex.Message));
            }
            catch (HttpRequestException ex)
            {
                return (null, responseContentString, string.Format(LanguageManager.GetString("ErrorNetworkRequest"), ex.Message));
            }
            catch (Exception ex)
            {
                return (null, responseContentString, string.Format(LanguageManager.GetString("ErrorProcessingSendingSegment"), ex.Message));
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }
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
}