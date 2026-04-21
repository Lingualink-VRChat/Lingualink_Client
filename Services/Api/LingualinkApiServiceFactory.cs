using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace lingualink_client.Services
{
    /// <summary>
    /// Lingualink API服务工厂
    /// 根据应用配置创建API服务实例
    /// </summary>
    public static class LingualinkApiServiceFactory
    {
        /// <summary>
        /// 根据应用设置创建API服务实例
        /// </summary>
        /// <param name="settings">应用设置</param>
        /// <returns>API服务实例</returns>
        public static ILingualinkApiService CreateApiService(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var (accessTokenProvider, unauthorizedHandler, terminologyProvider) = ResolveAuthContext();

            Debug.WriteLine($"[LingualinkApiServiceFactory] Creating API service with settings:");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   ActiveServerUrl: '{settings.ActiveServerUrl}'");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   UseOfficialAuthFlow: true");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   HasApiKey: false");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   HasTokenProvider: {accessTokenProvider != null}");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   OpusComplexity: {settings.OpusComplexity}");

            return new LingualinkApiService(
                serverUrl: settings.ActiveServerUrl,
                apiKey: string.Empty,
                accessTokenProvider: accessTokenProvider,
                unauthorizedHandler: unauthorizedHandler,
                terminologyProvider: terminologyProvider,
                opusComplexity: settings.OpusComplexity
            );
        }

        private static (Func<Task<string?>>? accessTokenProvider, Func<Task>? unauthorizedHandler, Func<Task<IReadOnlyList<CustomVocabularyEntry>>>? terminologyProvider) ResolveAuthContext()
        {
            Func<Task<IReadOnlyList<CustomVocabularyEntry>>>? terminologyProvider = null;

            if (ServiceContainer.TryResolve<ISettingsManager>(out var settingsManager) && settingsManager != null)
            {
                terminologyProvider = () => Task.FromResult(BuildActiveVocabularyEntries(settingsManager.LoadSettings()));
            }

            if (ServiceContainer.TryResolve<IAuthService>(out var authService) && authService != null)
            {
                return (authService.GetAccessTokenAsync, authService.HandleUnauthorizedAsync, terminologyProvider);
            }

            return (null, null, terminologyProvider);
        }

        private static IReadOnlyList<CustomVocabularyEntry> BuildActiveVocabularyEntries(AppSettings settings)
        {
            if (settings?.CustomVocabularyTables == null || settings.CustomVocabularyTables.Count == 0)
            {
                return Array.Empty<CustomVocabularyEntry>();
            }

            var activeTable = settings.CustomVocabularyTables
                .FirstOrDefault(table => table != null && table.Enabled && table.Entries != null);
            if (activeTable == null)
            {
                return Array.Empty<CustomVocabularyEntry>();
            }

            var entries = new List<CustomVocabularyEntry>();
            int totalCharacters = 0;

            foreach (var entry in activeTable.Entries)
            {
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                var term = AppSettings.NormalizeVocabularyTerm(entry.Term);
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                var aliases = AppSettings.NormalizeVocabularyValues(
                    entry.Aliases,
                    AppSettings.MaxAliasesPerVocabularyEntry,
                    AppSettings.MaxAliasesCharactersPerVocabularyEntry);
                var pronunciations = AppSettings.NormalizeVocabularyValues(
                    entry.Pronunciations,
                    AppSettings.MaxPronunciationsPerVocabularyEntry,
                    AppSettings.MaxPronunciationsCharactersPerVocabularyEntry);

                var entryCharacters = AppSettings.CountVocabularyEntryCharacters(term, aliases, pronunciations);
                if (entryCharacters <= 0)
                {
                    continue;
                }

                if (entries.Count >= AppSettings.MaxEntriesPerVocabularyTable
                    || entries.Count >= AppSettings.MaxCustomVocabularyPayloadEntries
                    || totalCharacters + entryCharacters > AppSettings.MaxCustomVocabularyTableCharacters)
                {
                    break;
                }

                entries.Add(new CustomVocabularyEntry
                {
                    Term = term,
                    Aliases = aliases,
                    Pronunciations = pronunciations,
                    Note = entry.Note,
                    Priority = entry.Priority,
                    Enabled = true
                });
                totalCharacters += entryCharacters;
            }

            return entries;
        }
    }
}
