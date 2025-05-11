// lingualink_client.Services.TranslationService.cs
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
        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        public TranslationService(string serverUrl)
        {
            _serverUrl = serverUrl;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<(ServerResponse? Response, string? ErrorMessage)> TranslateAudioSegmentAsync(
            byte[] audioData,
            WaveFormat waveFormat,
            string triggerReason,
            string targetLanguagesCsv)
        {
            if (audioData.Length == 0)
            {
                return (null, "Audio data is empty.");
            }

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"segment_{DateTime.Now:yyyyMMddHHmmssfff}_{triggerReason}.wav");
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
                    var responseContentString = await httpResponse.Content.ReadAsStringAsync();

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        try
                        {
                            var serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseContentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            return (serverResponse, null);
                        }
                        catch (JsonException ex)
                        {
                            return (null, $"Failed to deserialize server success response: {ex.Message}. Response: {responseContentString}");
                        }
                    }
                    else
                    {
                        try
                        {
                            var errorResponse = JsonSerializer.Deserialize<ServerResponse>(responseContentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                            {
                                return (errorResponse, $"Server error ({httpResponse.StatusCode}): {errorResponse.Message}");
                            }
                        }
                        catch { }
                        return (null, $"Server error ({httpResponse.StatusCode}): {responseContentString}");
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                return (null, $"Network request timed out: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Network request error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (null, $"Error processing/sending segment: {ex.Message}");
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