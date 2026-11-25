using NexaAI.Core.Application.Configuration;
using NexaAI.Core.Application.Interfaces;
using NexaAI.Core.Application.Services;
using NexaAI.Core.Infrastructure.Services;

namespace NexaAI.Api.Extensions;

/// <summary>Registers application-layer services and their configuration.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<AIServiceOptions>(configuration.GetSection(AIServiceOptions.SectionName));
        services.Configure<ChunkingOptions>(configuration.GetSection(ChunkingOptions.SectionName));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IEvaluationService, EvaluationService>();

        return services;
    }

    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Uses the named HttpClient configured in Program.cs with retry and circuit breaker policies.
        services.AddTransient<IAIServiceClient, AIServiceClient>();

        return services;
    }
}
