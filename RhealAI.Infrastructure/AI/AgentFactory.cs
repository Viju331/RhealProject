using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;

namespace RhealAI.Infrastructure.AI;

/// <summary>
/// Factory for creating chat clients using multiple AI providers
/// </summary>
public class AgentFactory
{
    private readonly IConfiguration _configuration;
    private const string DefaultModel = "gpt-4o-mini";

    public AgentFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Creates a chat client for code analysis
    /// </summary>
    public ChatClient CreateChatClient()
    {
        var provider = _configuration["AI:Provider"] ?? "Demo";

        return provider.ToLower() switch
        {
            "openai" => CreateOpenAIClient(),
            "github" => CreateGitHubModelsClient(),
            "demo" => CreateDemoClient(),
            _ => CreateDemoClient()
        };
    }

    private ChatClient CreateDemoClient()
    {
        // For demo mode, use OpenAI client with a fake key (won't actually call API)
        // We'll handle this in the services layer
        throw new InvalidOperationException("Demo mode - AI calls are mocked");
    }

    private ChatClient CreateOpenAIClient()
    {
        var apiKey = _configuration["AI:OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI API key not configured. Add it to appsettings.json under AI:OpenAI:ApiKey");

        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey));
        var model = _configuration["AI:OpenAI:Model"] ?? DefaultModel;

        return openAIClient.GetChatClient(model);
    }

    private ChatClient CreateGitHubModelsClient()
    {
        var githubToken = _configuration["AI:GitHub:Token"]
            ?? throw new InvalidOperationException("GitHub token not configured");

        var openAIClient = new OpenAIClient(
            new ApiKeyCredential(githubToken),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://models.inference.ai.azure.com")
            }
        );

        var model = _configuration["AI:GitHub:Model"] ?? DefaultModel;

        return openAIClient.GetChatClient(model);
    }

    /// <summary>
    /// Creates a specialized client for standards extraction
    /// </summary>
    public ChatClient CreateStandardsClient()
    {
        return CreateChatClient();
    }

    /// <summary>
    /// Creates a specialized client for bug detection
    /// </summary>
    public ChatClient CreateBugDetectionClient()
    {
        return CreateChatClient();
    }

    /// <summary>
    /// Creates a specialized client for violation detection
    /// </summary>
    public ChatClient CreateViolationDetectionClient()
    {
        return CreateChatClient();
    }
}
