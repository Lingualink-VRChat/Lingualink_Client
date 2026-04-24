using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace lingualink_client.Services
{
    public static class LogSanitizer
    {
        private const int DefaultMaxLength = 2048;

        private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "accesstoken",
            "refreshtoken",
            "idtoken",
            "token",
            "authorization",
            "xapikey",
            "apikey",
            "password",
            "confirmpassword",
            "code",
            "authcode",
            "audio",
            "text",
            "sourcetext",
            "correctedtext",
            "transcription",
            "rawresponse",
            "email",
            "avatarurl",
            "fingerprint",
            "qrcode",
            "loginurl"
        };

        private static readonly HashSet<string> SensitiveContainers = new(StringComparer.OrdinalIgnoreCase)
        {
            "translations",
            "user_dictionary"
        };

        public static string SanitizeJsonPayload(string? payload, int maxLength = DefaultMaxLength)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            try
            {
                var node = JsonNode.Parse(payload);
                if (node == null)
                {
                    return BuildReplacement(payload.Trim(), "payload");
                }

                SanitizeNode(node);
                return Truncate(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }), maxLength);
            }
            catch
            {
                return BuildReplacement(payload.Trim(), "payload");
            }
        }

        public static string SummarizeUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return Truncate(value.Trim(), 512);
            }

            var queryNames = uri.Query
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2)[0])
                .Where(name => !string.IsNullOrWhiteSpace(name));

            var querySummary = string.Join(",", queryNames);
            return string.IsNullOrWhiteSpace(querySummary)
                ? uri.GetLeftPart(UriPartial.Path)
                : $"{uri.GetLeftPart(UriPartial.Path)}?[{querySummary}]";
        }

        public static string DescribeValue(string? value, string label = "value")
        {
            return string.IsNullOrEmpty(value)
                ? $"{label}: empty"
                : $"{label}: [redacted, chars={value.Length}]";
        }

        private static void SanitizeNode(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                foreach (var property in obj.ToList())
                {
                    var key = property.Key;
                    var value = property.Value;

                    if (IsSensitiveKey(key))
                    {
                        obj[key] = BuildReplacement(value, key);
                        continue;
                    }

                    if (SensitiveContainers.Contains(key))
                    {
                        RedactStringValues(value);
                        continue;
                    }

                    if (value != null)
                    {
                        SanitizeNode(value);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item != null)
                    {
                        SanitizeNode(item);
                    }
                }
            }
        }

        private static void RedactStringValues(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (var property in obj.ToList())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        obj[property.Key] = BuildReplacement(text, property.Key);
                    }
                    else
                    {
                        RedactStringValues(property.Value);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        array[i] = BuildReplacement(text, "value");
                    }
                    else
                    {
                        RedactStringValues(array[i]);
                    }
                }
            }
        }

        private static string BuildReplacement(JsonNode? value, string key)
        {
            if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
            {
                return BuildReplacement(text, key);
            }

            return $"[redacted:{NormalizeKey(key)}]";
        }

        private static string BuildReplacement(string? value, string key)
        {
            var length = value?.Length ?? 0;
            return $"[redacted:{NormalizeKey(key)}, chars={length}]";
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? "value" : key.Trim().ToLowerInvariant();
        }

        private static bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var normalized = key
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Trim();
            return SensitiveKeys.Contains(normalized);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (maxLength <= 0 || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength] + $"...[truncated, chars={value.Length}]";
        }
    }
}
