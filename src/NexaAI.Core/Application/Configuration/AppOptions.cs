namespace NexaAI.Core.Application.Configuration;

/// <summary>Settings used to issue and validate JWT access tokens.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "NexaAI";
    public string Audience { get; set; } = "NexaAI";
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshExpiryDays { get; set; } = 7;
}

/// <summary>Local storage settings for uploaded documents.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string UploadPath { get; set; } = "./uploads";
    public int MaxFileSizeMB { get; set; } = 50;
}

/// <summary>Connection settings for the Python AI service.</summary>
public sealed class AIServiceOptions
{
    public const string SectionName = "AIService";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>Document chunking settings.</summary>
public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    public int ChunkSize { get; set; } = 1200;
    public int ChunkOverlap { get; set; } = 200;
}
