using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Application.Common.Images;
using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ItemRecognition.Infrastructure.Ai;

public sealed class GigaChatAiVisionClient(
    HttpClient httpClient,
    GigaChatOptions options,
    IAiCallRepository aiCallRepository,
    IUnitOfWork unitOfWork,
    ILogger<GigaChatAiVisionClient> logger)
    : IAiVisionClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly string _clientId = string.IsNullOrWhiteSpace(options.ClientId)
        ? Guid.NewGuid().ToString()
        : options.ClientId.Trim();

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAtUtc = DateTimeOffset.MinValue;
    private bool _disposed;

    public Task<AiResult<IReadOnlyList<MainObjectPrediction>>> DetectMainObjectsAsync(
        Guid requestId,
        DownloadedImage image,
        string promptVersion,
        string promptText,
        CancellationToken cancellationToken = default) =>
        ExecuteStageAsync(
            requestId,
            AiStage.MainObjects,
            image,
            promptVersion,
            promptText,
            null,
            ParseMainObjects,
            cancellationToken);

    public Task<AiResult<IReadOnlyList<MaterialsPrediction>>> DetectMaterialsAsync(
        Guid requestId,
        DownloadedImage image,
        IReadOnlyList<string> itemNames,
        string promptVersion,
        string promptText,
        CancellationToken cancellationToken = default) =>
        ExecuteStageAsync(
            requestId,
            AiStage.Materials,
            image,
            promptVersion,
            promptText,
            itemNames,
            ParseMaterials,
            cancellationToken);

    private async Task<AiResult<IReadOnlyList<TPrediction>>> ExecuteStageAsync<TPrediction>(
        Guid requestId,
        AiStage stage,
        DownloadedImage image,
        string promptVersion,
        string promptText,
        IReadOnlyList<string>? itemNames,
        Func<string, IReadOnlyList<TPrediction>> parser,
        CancellationToken cancellationToken)
    {
        ValidateCallInput(requestId, image, promptVersion, promptText);

        var stopwatch = Stopwatch.StartNew();
        var requestPayloadJson = "{}";
        var responseJson = "{}";
        string? accessToken = null;
        string? uploadedFileId = null;

        AiResult<IReadOnlyList<TPrediction>>? result = null;

        try
        {
            accessToken = await GetAccessTokenAsync(cancellationToken);

            if (options.UseAttachmentsForImages)
            {
                var uploadResult = await UploadImageAsync(accessToken, image, cancellationToken);
                if (!uploadResult.IsSuccess)
                {
                    responseJson = uploadResult.ResponseJson;
                    result = AiResult<IReadOnlyList<TPrediction>>.Failure(
                        uploadResult.Error!.Value.Code,
                        uploadResult.Error.Value.Message);
                }
                else
                {
                    uploadedFileId = uploadResult.FileId;
                }
            }

            if (result is null)
            {
                var requestPayload = BuildChatPayload(image, promptText, itemNames, uploadedFileId);
                requestPayloadJson = requestPayload.ToJsonString(JsonOptions);

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, options.ChatCompletionsUrl);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                AddClientIdHeader(requestMessage.Headers);
                requestMessage.Content = new StringContent(requestPayloadJson, Encoding.UTF8, "application/json");

                using var response = await httpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                responseJson = NormalizeJson(rawResponse);

                if (!response.IsSuccessStatusCode)
                {
                    var error = BuildHttpError(response.StatusCode, rawResponse);
                    result = AiResult<IReadOnlyList<TPrediction>>.Failure(error.Code, error.Message);
                }
                else
                {
                    var assistantContent = ExtractAssistantContent(rawResponse);
                    var assistantJson = ExtractJsonFromText(assistantContent);
                    var value = parser(assistantJson);
                    result = AiResult<IReadOnlyList<TPrediction>>.Success(value);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = AiResult<IReadOnlyList<TPrediction>>.Failure("ai_timeout", "AI request timed out.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse AI response.");
            result = AiResult<IReadOnlyList<TPrediction>>.Failure("ai_invalid_response", "AI returned invalid JSON response.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP error while calling AI provider.");
            result = AiResult<IReadOnlyList<TPrediction>>.Failure("ai_transport_error", "Failed to call AI provider.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected AI client error.");
            result = AiResult<IReadOnlyList<TPrediction>>.Failure("ai_client_error", ex.Message);
        }
        finally
        {
            if (options.UseAttachmentsForImages &&
                options.DeleteUploadedFilesAfterRequest &&
                !string.IsNullOrWhiteSpace(accessToken) &&
                !string.IsNullOrWhiteSpace(uploadedFileId))
            {
                await TryDeleteUploadedFileAsync(accessToken!, uploadedFileId!, CancellationToken.None);
            }
        }

        result ??= AiResult<IReadOnlyList<TPrediction>>.Failure(
            "ai_client_error",
            "AI client completed without a result.");

        stopwatch.Stop();

        await PersistAiCallAsync(
            requestId,
            stage,
            promptVersion,
            promptText,
            requestPayloadJson,
            responseJson,
            result,
            stopwatch.ElapsedMilliseconds,
            cancellationToken);

        return result;
    }

    private async Task PersistAiCallAsync<TPrediction>(
        Guid requestId,
        AiStage stage,
        string promptVersion,
        string promptText,
        string requestPayloadJson,
        string responseJson,
        AiResult<IReadOnlyList<TPrediction>> result,
        long durationMs,
        CancellationToken cancellationToken)
    {
        var aiCall = new AiCall
        {
            RequestId = requestId,
            Stage = stage,
            Provider = options.Provider,
            Model = options.Model,
            PromptVersion = promptVersion,
            PromptText = promptText,
            RequestPayload = EnsureJsonObject(requestPayloadJson),
            ResponseJson = EnsureJsonObject(responseJson),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.Error?.Message,
            DurationMs = durationMs > int.MaxValue ? int.MaxValue : (int)durationMs
        };

        await aiCallRepository.AddAsync(aiCall, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (HasValidAccessToken())
        {
            return _accessToken!;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (HasValidAccessToken())
            {
                return _accessToken!;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, options.AuthUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("RqUID", Guid.NewGuid().ToString());
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["scope"] = options.Scope });

            var authHeader = options.AuthorizationKey.Trim();
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                throw new InvalidOperationException("GigaChat authorization key is not configured.");
            }

            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
            }

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = BuildHttpError(response.StatusCode, rawResponse);
                throw new InvalidOperationException($"Failed to get GigaChat token: {error.Message}");
            }

            using var tokenJson = JsonDocument.Parse(rawResponse);
            var root = tokenJson.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(tokenElement.GetString()))
            {
                throw new InvalidOperationException("GigaChat token response does not contain access_token.");
            }

            _accessToken = tokenElement.GetString();
            _accessTokenExpiresAtUtc = ParseTokenExpiry(root);

            return _accessToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool HasValidAccessToken()
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return false;
        }

        return _accessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(options.TokenRefreshSkewSeconds);
    }

    private static DateTimeOffset ParseTokenExpiry(JsonElement tokenRoot)
    {
        if (!tokenRoot.TryGetProperty("expires_at", out var expiresElement))
        {
            return DateTimeOffset.UtcNow.AddMinutes(25);
        }

        long rawValue;
        switch (expiresElement.ValueKind)
        {
            case JsonValueKind.Number when expiresElement.TryGetInt64(out var number):
                rawValue = number;
                break;
            case JsonValueKind.String when long.TryParse(expiresElement.GetString(), out var fromString):
                rawValue = fromString;
                break;
            default:
                return DateTimeOffset.UtcNow.AddMinutes(25);
        }

        try
        {
            // New API returns milliseconds, but some examples still show seconds.
            return rawValue > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(rawValue)
                : DateTimeOffset.FromUnixTimeSeconds(rawValue);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.UtcNow.AddMinutes(25);
        }
    }

    private JsonObject BuildChatPayload(
        DownloadedImage image,
        string promptText,
        IReadOnlyList<string>? itemNames,
        string? uploadedFileId)
    {
        JsonNode messageContent;
        JsonArray? attachments = null;

        if (options.UseAttachmentsForImages)
        {
            if (string.IsNullOrWhiteSpace(uploadedFileId))
            {
                throw new InvalidOperationException("Uploaded image id is required when attachments mode is enabled.");
            }

            messageContent = promptText;
            attachments = new JsonArray(uploadedFileId);
        }
        else
        {
            var imageBase64 = Convert.ToBase64String(image.Content);
            var dataUrl = $"data:{image.ContentType};base64,{imageBase64}";
            messageContent = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = promptText
                },
                new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject
                    {
                        ["url"] = dataUrl
                    }
                }
            };
        }

        var primaryMessage = new JsonObject
        {
            ["role"] = "user",
            ["content"] = messageContent
        };

        if (attachments is not null)
        {
            primaryMessage["attachments"] = attachments;
        }

        var messages = new JsonArray { primaryMessage };

        if (itemNames is { Count: > 0 })
        {
            messages.Add(
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = $"Confirmed items JSON: {JsonSerializer.Serialize(itemNames, JsonOptions)}"
                });
        }

        var payload = new JsonObject
        {
            ["model"] = options.Model,
            ["messages"] = messages,
            ["stream"] = false
        };

        if (options.Temperature.HasValue)
        {
            payload["temperature"] = options.Temperature.Value;
        }

        if (options.TopP.HasValue)
        {
            payload["top_p"] = options.TopP.Value;
        }

        if (options.MaxTokens.HasValue)
        {
            payload["max_tokens"] = options.MaxTokens.Value;
        }

        return payload;
    }

    private async Task<FileUploadResult> UploadImageAsync(
        string accessToken,
        DownloadedImage image,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(image.Content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType);
        form.Add(fileContent, "file", BuildFileName(image.ContentType));
        form.Add(new StringContent(options.FilesUploadPurpose), "purpose");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, options.FilesUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        AddClientIdHeader(requestMessage.Headers);
        requestMessage.Content = form;

        using var response = await httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var normalizedResponse = NormalizeJson(rawResponse);

        if (!response.IsSuccessStatusCode)
        {
            var error = BuildHttpError(response.StatusCode, rawResponse);
            return new FileUploadResult(false, null, normalizedResponse, error);
        }

        if (!TryExtractFileId(rawResponse, out var fileId))
        {
            return new FileUploadResult(
                false,
                null,
                normalizedResponse,
                ("ai_invalid_response", "AI provider file upload response does not contain file id."));
        }

        return new FileUploadResult(true, fileId, normalizedResponse, null);
    }

    private async Task TryDeleteUploadedFileAsync(string accessToken, string fileId, CancellationToken cancellationToken)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BuildDeleteFileUrl(fileId));
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            AddClientIdHeader(requestMessage.Headers);

            using var response = await httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogDebug(
                "Failed to delete uploaded GigaChat file {FileId}. HTTP {StatusCode}. Response: {Response}",
                fileId,
                (int)response.StatusCode,
                rawResponse);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to delete uploaded GigaChat file {FileId}.", fileId);
        }
    }

    private static bool TryExtractFileId(string rawResponse, out string fileId)
    {
        fileId = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var root = document.RootElement;

            return TryReadStringProperty(root, "id", out fileId) ||
                   TryReadStringProperty(root, "file_id", out fileId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var parsed = propertyElement.GetString();
        if (string.IsNullOrWhiteSpace(parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string BuildFileName(string contentType) =>
        contentType switch
        {
            "image/jpeg" => "image.jpg",
            "image/png" => "image.png",
            "image/webp" => "image.webp",
            _ => "image.bin"
        };

    private string BuildDeleteFileUrl(string fileId) =>
        $"{options.FilesUrl.TrimEnd('/')}/{Uri.EscapeDataString(fileId)}/delete";

    private void AddClientIdHeader(HttpRequestHeaders headers)
    {
        if (string.IsNullOrWhiteSpace(_clientId))
        {
            return;
        }

        headers.TryAddWithoutValidation("X-Client-ID", _clientId);
    }

    private static string ExtractAssistantContent(string rawResponse)
    {
        using var responseJson = JsonDocument.Parse(rawResponse);
        var root = responseJson.RootElement;

        if (!root.TryGetProperty("choices", out var choicesElement) ||
            choicesElement.ValueKind != JsonValueKind.Array ||
            choicesElement.GetArrayLength() == 0)
        {
            throw new JsonException("AI response does not contain choices.");
        }

        var choice = choicesElement[0];
        if (!choice.TryGetProperty("message", out var messageElement))
        {
            throw new JsonException("AI response does not contain message.");
        }

        if (!messageElement.TryGetProperty("content", out var contentElement))
        {
            throw new JsonException("AI response does not contain message content.");
        }

        return contentElement.ValueKind switch
        {
            JsonValueKind.String => contentElement.GetString() ?? string.Empty,
            JsonValueKind.Array => ExtractTextFromArrayContent(contentElement),
            _ => throw new JsonException("Unsupported AI response content format.")
        };
    }

    private static string ExtractTextFromArrayContent(JsonElement contentArray)
    {
        var chunks = new List<string>();
        foreach (var part in contentArray.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                var value = part.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    chunks.Add(value);
                }

                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (part.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(textElement.GetString()))
            {
                chunks.Add(textElement.GetString()!);
            }
        }

        return chunks.Count == 0 ? throw new JsonException("AI message content array does not contain text.") : string.Join('\n', chunks);
    }

    private static string ExtractJsonFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException("AI content is empty.");
        }

        var normalized = StripCodeFence(text.Trim());
        if (TryNormalizeJson(normalized, out var json) ||
            TryNormalizeJsonSlice(normalized, '{', '}', out json) ||
            TryNormalizeJsonSlice(normalized, '[', ']', out json))
        {
            return json;
        }

        throw new JsonException("AI content does not contain valid JSON.");
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return text;
        }

        var closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
        return closingFence <= firstLineEnd ? text : text[(firstLineEnd + 1)..closingFence].Trim();
    }

    private static bool TryNormalizeJson(string candidate, out string json)
    {
        json = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(candidate);
            json = document.RootElement.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryNormalizeJsonSlice(string text, char openChar, char closeChar, out string json)
    {
        json = string.Empty;

        var startIndex = text.IndexOf(openChar);
        var endIndex = text.LastIndexOf(closeChar);
        if (startIndex < 0 || endIndex <= startIndex)
        {
            return false;
        }

        var candidate = text[startIndex..(endIndex + 1)];
        return TryNormalizeJson(candidate, out json);
    }

    private static IReadOnlyList<MainObjectPrediction> ParseMainObjects(string assistantJson)
    {
        using var document = JsonDocument.Parse(assistantJson);
        if (!document.RootElement.TryGetProperty("objects", out var objectsElement) ||
            objectsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Main objects JSON must contain objects array.");
        }

        var result = new List<MainObjectPrediction>();
        var fallbackRank = 1;

        foreach (var item in objectsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                fallbackRank++;
                continue;
            }

            var name = nameElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                fallbackRank++;
                continue;
            }

            var isPrimary = item.TryGetProperty("isPrimary", out var isPrimaryElement) &&
                            (isPrimaryElement.ValueKind is JsonValueKind.True or JsonValueKind.False) && isPrimaryElement.GetBoolean();

            float? confidence = null;
            if (item.TryGetProperty("confidence", out var confidenceElement) &&
                confidenceElement.ValueKind == JsonValueKind.Number &&
                confidenceElement.TryGetDouble(out var rawConfidence) &&
                rawConfidence is >= 0d and <= 1d)
            {
                confidence = (float)rawConfidence;
            }

            var rank = item.TryGetProperty("rank", out var rankElement) &&
                       rankElement.ValueKind == JsonValueKind.Number &&
                       rankElement.TryGetInt32(out var rawRank) &&
                       rawRank >= 1
                ? rawRank
                : fallbackRank;

            result.Add(new MainObjectPrediction(name, isPrimary, confidence, rank));
            fallbackRank++;
        }

        if (result.Count == 0)
        {
            throw new JsonException("Main objects array is empty or invalid.");
        }

        return result
            .OrderBy(prediction => prediction.Rank)
            .ToList();
    }

    private static IReadOnlyList<MaterialsPrediction> ParseMaterials(string assistantJson)
    {
        using var document = JsonDocument.Parse(assistantJson);
        if (!document.RootElement.TryGetProperty("items", out var itemsElement) ||
            itemsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Materials JSON must contain items array.");
        }

        var result = new List<MaterialsPrediction>();

        foreach (var item in itemsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var materials = new List<string>();
            var seenMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (item.TryGetProperty("materials", out var materialsElement) &&
                materialsElement.ValueKind == JsonValueKind.Array)
            {
                materials.AddRange(from materialElement in materialsElement.EnumerateArray() where materialElement.ValueKind == JsonValueKind.String select materialElement.GetString()?.Trim() into materialName where !string.IsNullOrWhiteSpace(materialName) && seenMaterials.Add(materialName) select materialName);
            }

            result.Add(new MaterialsPrediction(name, materials));
        }

        return result.Count == 0 ? throw new JsonException("Materials items array is empty or invalid.") : result;
    }

    private static (string Code, string Message) BuildHttpError(HttpStatusCode statusCode, string responseBody)
    {
        var details = TryExtractErrorMessage(responseBody);
        var baseMessage = $"AI provider returned HTTP {(int)statusCode}.";
        var message = string.IsNullOrWhiteSpace(details) ? baseMessage : $"{baseMessage} {details}";

        return (Code: "ai_http_error", Message: message);
    }

    private static string? TryExtractErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }

            if (root.TryGetProperty("error", out var errorElement))
            {
                if (errorElement.ValueKind == JsonValueKind.String)
                {
                    return errorElement.GetString();
                }

                if (errorElement.ValueKind == JsonValueKind.Object &&
                    errorElement.TryGetProperty("message", out var nestedMessageElement) &&
                    nestedMessageElement.ValueKind == JsonValueKind.String)
                {
                    return nestedMessageElement.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // ignored
        }

        return responseBody.Trim();
    }

    private static string NormalizeJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "{}";
        }

        return TryNormalizeJson(payload, out var json)
            ? json
            : JsonSerializer.Serialize(new { raw = payload }, JsonOptions);
    }

    private static string EnsureJsonObject(string payload)
    {
        return TryNormalizeJson(payload, out var json) ? json : "{}";
    }

    private readonly record struct FileUploadResult(
        bool IsSuccess,
        string? FileId,
        string ResponseJson,
        (string Code, string Message)? Error);

    private static void ValidateCallInput(
        Guid requestId,
        DownloadedImage image,
        string promptVersion,
        string promptText)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(image);

        if (image.Content.Length == 0)
        {
            throw new ArgumentException("Image content is empty.", nameof(image));
        }

        if (string.IsNullOrWhiteSpace(promptVersion))
        {
            throw new ArgumentException("Prompt version is required.", nameof(promptVersion));
        }

        if (string.IsNullOrWhiteSpace(promptText))
        {
            throw new ArgumentException("Prompt text is required.", nameof(promptText));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _tokenLock.Dispose();
        _disposed = true;
    }
}
