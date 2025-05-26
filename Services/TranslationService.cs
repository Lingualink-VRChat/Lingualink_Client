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
        private bool _disposed = false;
 
        public TranslationService(string serverUrl, string apiKey = "")
        {
            _serverUrl = serverUrl;
            _apiKey = apiKey;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            
            // Always attempt to set up authentication headers if an API key is provided
            if (!string.IsNullOrEmpty(_apiKey))
            {
                // Use X-API-Key header (preferred by the new backend)
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
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

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"segment_{DateTime.Now:yyyyMMddHHmmssfff}_{triggerReason}.wav");
            string? responseContentString = null; // To store raw JSON
            try
            {
                await using (var writer = new WaveFileWriter(tempFilePath, waveFormat))
                {
                    await writer.WriteAsync(audioData, 0, audioData.Length);
                }

                using (var formData = new MultipartFormDataContent())
                {
                    var fileBytes = await File.ReadAllBytesAsync(tempFilePath);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                    formData.Add(fileContent, "audio_file", Path.GetFileName(tempFilePath));

                    // Add user_prompt field as required by new API
                    formData.Add(new StringContent("请处理下面的音频。"), "user_prompt");
                    Debug.WriteLine($"[DEBUG] TranslationService: 添加 'user_prompt' = '请处理下面的音频。'");

                    if (!string.IsNullOrWhiteSpace(targetLanguagesCsv))
                    {
                        var languagesList = targetLanguagesCsv.Split(',')
                                                              .Select(lang => lang.Trim())
                                                              .Where(lang => !string.IsNullOrWhiteSpace(lang));
                        foreach (var lang in languagesList)
                        {
                            Debug.WriteLine($"[DEBUG] TranslationService: 添加 'target_languages' = '{lang}'");
                            formData.Add(new StringContent(lang), "target_languages");
                        }
                    }

                    var httpResponse = await _httpClient.PostAsync(_serverUrl, formData);
                    responseContentString = await httpResponse.Content.ReadAsStringAsync(); // Get raw JSON

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