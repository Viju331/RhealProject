using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using OpenAI.Chat;

namespace RhealAI.Infrastructure.AI;

/// <summary>
/// Adapter that wraps Google Gemini API to work with OpenAI ChatClient interface
/// </summary>
public class GeminiChatClientAdapter : ChatClient
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;

    public GeminiChatClientAdapter(string apiKey, string model)
    {
        _apiKey = apiKey;
        _model = model;
        _httpClient = new HttpClient();
    }

    public override async Task<ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert OpenAI messages to Gemini format
            var geminiContents = new List<object>();
            string? systemInstruction = null;

            foreach (var message in messages)
            {
                if (message is SystemChatMessage systemMsg)
                {
                    systemInstruction = systemMsg.Content[0].Text;
                }
                else if (message is UserChatMessage userMsg)
                {
                    geminiContents.Add(new
                    {
                        role = "user",
                        parts = new[] { new { text = userMsg.Content[0].Text } }
                    });
                }
                else if (message is AssistantChatMessage assistantMsg)
                {
                    geminiContents.Add(new
                    {
                        role = "model", // Gemini uses "model" instead of "assistant"
                        parts = new[] { new { text = assistantMsg.Content[0].Text } }
                    });
                }
            }

            // If there's a system instruction, prepend it to the first user message
            if (!string.IsNullOrEmpty(systemInstruction) && geminiContents.Count > 0)
            {
                var firstMessage = geminiContents[0] as dynamic;
                if (firstMessage != null)
                {
                    var originalText = ((dynamic)firstMessage.parts[0]).text;
                    geminiContents[0] = new
                    {
                        role = "user",
                        parts = new[] { new { text = $"{systemInstruction}\n\n{originalText}" } }
                    };
                }
            }

            // Build Gemini request
            var request = new
            {
                contents = geminiContents,
                generationConfig = new
                {
                    temperature = options?.Temperature ?? 1.0f,
                    maxOutputTokens = options?.MaxOutputTokenCount ?? 8192,
                    topP = options?.TopP ?? 0.95f
                }
            };

            // Call Gemini API
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Gemini API call failed: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GeminiResponse>(responseContent);

            // Extract response text
            var content = result?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text ?? "";
            var inputTokens = result?.usageMetadata?.promptTokenCount ?? 0;
            var outputTokens = result?.usageMetadata?.candidatesTokenCount ?? 0;

            var chatCompletion = CreateChatCompletion(content, inputTokens, outputTokens);

            return ClientResult.FromValue(chatCompletion, new MockPipelineResponse());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Gemini API call failed: {ex.Message}", ex);
        }
    }

    public override ClientResult<ChatCompletion> CompleteChat(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return CompleteChatAsync(messages, options, cancellationToken).GetAwaiter().GetResult();
    }

    private ChatCompletion CreateChatCompletion(string content, int inputTokens, int outputTokens)
    {
        // Create ChatTokenUsage using reflection
        var usageType = typeof(ChatTokenUsage);
        var usageConstructor = usageType.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)[0];
        var usage = (ChatTokenUsage)usageConstructor.Invoke(new object[] { outputTokens, inputTokens, inputTokens + outputTokens });

        // Create a minimal ChatCompletion object using reflection
        var chatCompletionType = typeof(ChatCompletion);
        var constructor = chatCompletionType.GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];

        var chatCompletion = (ChatCompletion)constructor.Invoke(new object?[]
        {
            Guid.NewGuid().ToString(), // id
            DateTimeOffset.UtcNow,      // createdAt
            _model,                      // model
            new[] { ChatMessageContentPart.CreateTextPart(content) }, // content
            null,                        // toolCalls
            ChatFinishReason.Stop,      // finishReason
            usage                        // usage
        });

        return chatCompletion;
    }
}

// Response models for Gemini API
internal class GeminiResponse
{
    public List<GeminiCandidate>? candidates { get; set; }
    public GeminiUsageMetadata? usageMetadata { get; set; }
}

internal class GeminiCandidate
{
    public GeminiContent? content { get; set; }
}

internal class GeminiContent
{
    public List<GeminiPart>? parts { get; set; }
}

internal class GeminiPart
{
    public string? text { get; set; }
}

internal class GeminiUsageMetadata
{
    public int promptTokenCount { get; set; }
    public int candidatesTokenCount { get; set; }
    public int totalTokenCount { get; set; }
}

/// <summary>
/// Mock pipeline response for compatibility
/// </summary>
internal class MockPipelineResponse : PipelineResponse
{
    public override int Status => 200;
    public override string ReasonPhrase => "OK";
    public override Stream? ContentStream { get; set; }
    public override BinaryData Content => BinaryData.FromString("{}");

    protected override PipelineResponseHeaders HeadersCore => new MockHeaders();

    public override BinaryData BufferContent(CancellationToken cancellationToken = default) => Content;

    public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) => new ValueTask<BinaryData>(Content);

    public override void Dispose() { }
}

internal class MockHeaders : PipelineResponseHeaders
{
    public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        yield break;
    }

    public override bool TryGetValue(string name, out string? value)
    {
        value = null;
        return false;
    }

    public override bool TryGetValues(string name, out IEnumerable<string>? values)
    {
        values = null;
        return false;
    }
}
