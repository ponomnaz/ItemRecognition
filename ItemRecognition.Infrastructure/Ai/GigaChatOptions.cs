namespace ItemRecognition.Infrastructure.Ai;

public sealed class GigaChatOptions
{
    public const string SectionName = "GigaChat";

    public string Provider { get; set; } = "gigachat";
    public string Model { get; set; } = "GigaChat-2-Max";
    public string Scope { get; set; } = "GIGACHAT_API_PERS";
    public string AuthorizationKey { get; set; } = string.Empty;

    public string AuthUrl { get; set; } = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    public string ChatCompletionsUrl { get; set; } = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
    public string FilesUrl { get; set; } = "https://gigachat.devices.sberbank.ru/api/v1/files";
    public string FilesUploadPurpose { get; set; } = "general";
    public bool UseAttachmentsForImages { get; set; } = true;
    public bool DeleteUploadedFilesAfterRequest { get; set; } = false;
    public string? ClientId { get; set; }

    public double? Temperature { get; set; } = 0.0d;
    public double? TopP { get; set; } = 0.1d;
    public int? MaxTokens { get; set; }

    public int TokenRefreshSkewSeconds { get; set; } = 60;
    public int RequestTimeoutSeconds { get; set; } = 100;
}
