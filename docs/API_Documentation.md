
# Lingualink Core API Documentation (v2.0)

## 1. Overview

Welcome to the Lingualink Core API v2.0. This document provides a comprehensive guide for developers integrating with our high-performance, multi-modal processing service. The API is designed to be RESTful, predictable, and robust, featuring graceful degradation for non-ideal LLM responses.

-   **API Version**: `v2.0`
-   **Base URL**: `http://localhost:8080/api/v1`
-   **Response Format**: `application/json`

## 2. Authentication

All protected endpoints require authentication via an **API Key**.

Provide the key in the request header:
```http
X-API-Key: your-api-key-here
```

## 3. Core Concepts

### Task Types

| Task           | Endpoint                | Description                                        |
| :------------- | :---------------------- | :------------------------------------------------- |
| `transcribe`   | `/process_audio`        | Transcribes audio into its original language.      |
| `translate`    | `/process_audio`        | Transcribes audio and then translates it to target languages. |
| `translate`    | `/process_text`         | Translates text to target languages.               |

### Language Codes

The API uses standard IETF language tags (e.g., `en`, `ja`, `zh`, `zh-hant`). You can retrieve a full list of supported languages from the `/languages` endpoint.

### Graceful Fallbacks & Partial Success

LLMs, while powerful, may not always follow formatting instructions perfectly. Instead of returning a hard error, **Lingualink Core is designed to gracefully degrade**. When the system cannot parse the LLM's response into the expected structured format, it activates a **fallback mechanism**.

This results in a successful `200 OK` HTTP response but with a special status:
-   **`status`: "partial_success"**: Indicates that the primary operation succeeded, but the response structure is based on a fallback.
-   **`metadata.fallback_mode`: true**: A flag confirming that fallback logic was used.
-   **`metadata.fallback_reason`**: A string explaining why the fallback was triggered.

This design ensures you always get the most valuable data possible, even in imperfect conditions. See Section 5 for a detailed guide on handling this.

## 4. Endpoints

---

### **GET /health**

Checks the operational status of the service.
-   **Authentication**: None
-   **Example**: `curl "http://localhost:8080/api/v1/health"`
-   **Response**: `{"status": "healthy", "version": "1.0.0", ...}`

---

### **GET /capabilities**

Retrieves the system's supported features, formats, and limits.
-   **Authentication**: Required
-   **Example**: `curl -H "X-API-Key: ..." "http://localhost:8080/api/v1/capabilities"`

---

### **GET /languages**

Returns a detailed list of all supported languages.
-   **Authentication**: Required
-   **Example**: `curl -H "X-API-Key: ..." "http://localhost:8080/api/v1/languages"`

---

### **POST /process_audio**

Performs transcription and/or translation on an audio file.
-   **Authentication**: Required
-   **Content-Type**: `application/json`

#### Request Body

| Field                | Type                | Required                               | Description                                                                 |
| :------------------- | :------------------ | :------------------------------------- | :-------------------------------------------------------------------------- |
| `audio`              | `string`            | **Yes**                                | Base64 encoded audio data.                                                  |
| `audio_format`       | `string`            | **Yes**                                | The original format of the audio (e.g., `opus`, `mp3`, `wav`).              |
| `task`               | `string`            | **Yes**                                | Task to perform: `transcribe` or `translate`.                               |
| `target_languages`   | `array of strings`  | **Yes**, if `task` is `translate`      | An array of language codes to translate the text into.                      |

#### Response Body

| Field                | Type                | Description                                                                              |
| :------------------- | :------------------ | :--------------------------------------------------------------------------------------- |
| `request_id`         | `string`            | A unique ID for this request, prefixed with `req_`.                                      |
| `status`             | `string`            | **`success`**, **`partial_success`**, or `failed`. See Section 5 for details.            |
| `transcription`      | `string`            | The transcribed text from the audio. May be populated by fallback logic.                 |
| `translations`       | `object`            | A map of translations. May be populated by fallback logic.                               |
| `raw_response`       | `string`            | The raw, unparsed text response from the LLM. Crucial for debugging fallbacks.           |
| `metadata`           | `object`            | Contains LLM info, token counts, and **fallback details** if applicable.                 |

---

### **POST /process_text**

Performs translation on a text string.
-   **Authentication**: Required
-   **Content-Type**: `application/json`

#### Request Body

| Field                | Type                | Required | Description                                                                   |
| :------------------- | :------------------ | :------- | :---------------------------------------------------------------------------- |
| `text`               | `string`            | **Yes**  | The text string to be translated.                                             |
| `target_languages`   | `array of strings`  | **Yes**  | An array of language codes to translate the text into.                        |

#### Response Body

The response body structure is similar to `/process_audio`, but with `source_text` instead of `transcription` and a `request_id` prefixed with `txt_`. It is also subject to the same fallback logic.

## 5. Fallback Logic & Partial Success

This section details how to work with the API's robust recovery mechanism.

### Triggers for Fallback

Fallback logic is activated when the LLM's response is received but cannot be parsed into the expected structured format. Common triggers include:
-   The LLM ignores formatting and returns a plain block of text.
-   Required sections (like `原文:`) are missing from the response.
-   Language keys (like `英文:`) do not match the expected patterns.
-   No translations are found in the response, despite being requested.

### How It Works: The `partial_success` State

When a fallback occurs, the API returns `HTTP 200 OK` with the following indicators:

-   `status`: `"partial_success"`
-   `metadata.fallback_mode`: `true`
-   `metadata.fallback_reason`: A message like `"LLM returned unstructured response..."`

The system then intelligently uses the `raw_response` to populate the primary data fields as best as it can.

### Scenario 1: Audio Processing Fallback

Imagine you request a translation to English (`en`), but the LLM responds without proper formatting.

-   **Raw LLM Response**:
    ```text
    Hello, this is a test of the audio system.
    ```
-   **API Response (`partial_success`)**:
    ```json
    {
      "request_id": "req_1720275899818816000",
      "status": "partial_success",
      // Fallback: The system assumes the raw response is the transcription.
      "transcription": "Hello, this is a test of the audio system.",
      "translations": {
        // Fallback: It also uses the raw response as the translation for the first target language.
        "en": "Hello, this is a test of the audio system."
      },
      "raw_response": "Hello, this is a test of the audio system.",
      "processing_time": 2.15,
      "metadata": {
        "model": "qwen-v1.5-32b",
        "backend": "vllm-default",
        "fallback_mode": true,
        "fallback_reason": "using raw content as transcription; using raw content as translation for en"
      }
    }
    ```

### Scenario 2: Text Processing Fallback

You request a translation of "Hello" to Chinese (`zh`), but the LLM just returns the answer directly.

-   **Raw LLM Response**:
    ```text
    你好
    ```
-   **API Response (`partial_success`)**:
    ```json
    {
      "request_id": "txt_1720276135017081000",
      "status": "partial_success",
      "source_text": "Hello",
      "translations": {
        // Fallback: The raw response is used as the translation for the first target language.
        "zh": "你好"
      },
      "raw_response": "你好",
      "processing_time": 0.55,
      "metadata": {
        "model": "qwen-v1.5-32b",
        "backend": "vllm-default",
        "fallback_mode": true,
        "fallback_reason": "LLM returned unstructured response, using as translation for first target language"
      }
    }
    ```

### How to Handle `partial_success`

As a developer, you should build your client to recognize this state.

1.  **Always check the `status` field** in the response.
2.  If `status === "partial_success"`, you can still use the `transcription` and `translations` fields, but be aware they might be less reliable.
3.  You may want to display a small indicator in your UI (e.g., "Result might be incomplete").
4.  Log the `metadata.fallback_reason` and `raw_response` to help debug issues with the LLM prompts or models.

#### Client-side Pseudocode

```javascript
async function handleApiResponse(response) {
  const data = response.data;

  if (data.status === 'success') {
    // Perfect result, display normally.
    console.log('Transcription:', data.transcription);
    console.log('Translations:', data.translations);
  } else if (data.status === 'partial_success') {
    // Fallback result, handle with care.
    console.warn('Partial success. Fallback was used.');
    console.warn('Reason:', data.metadata.fallback_reason);
    
    // You can still use the data, but maybe show a UI warning.
    displayResult(data.transcription, data.translations, { isPartial: true });
    
    // Log for analytics/debugging.
    logToServer({
      requestId: data.request_id,
      rawResponse: data.raw_response
    });
  } else {
    // Handle 'failed' status if applicable.
    console.error('Processing failed.');
  }
}
```

## 6. Error Handling

For hard failures (e.g., invalid input, auth errors), the API uses standard HTTP status codes. These are distinct from the `200 OK` with `partial_success` status.

| Code | Meaning                 | Example Reason                                              |
| :--- | :---------------------- | :---------------------------------------------------------- |
| `400`  | Bad Request             | `"target_languages are required"` for a `translate` task.   |
| `401`  | Unauthorized            | `"authentication failed"`                                   |
| `413`  | Payload Too Large       | `"audio size (40MB) exceeds maximum allowed size (32MB)"` |
| `500`  | Internal Server Error   | `"llm process failed: backend process failed..."`         |

#### Error Response Format

```json
{
  "error": "A descriptive error message."
}
```