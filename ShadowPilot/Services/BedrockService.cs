using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ShadowPilot.Models;

namespace ShadowPilot.Services;

// AWS Bedrock Converse API using x-amzn-api-key authentication
public class BedrockService
{
    private readonly string _apiKey;
    private readonly string _region;
    private readonly string _modelId;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };

    public BedrockService(string apiKey, string region = "us-east-1",
        string modelId = "us.meta.llama3-3-70b-instruct-v1:0")
    {
        _apiKey  = apiKey;
        _region  = region;
        _modelId = modelId;
    }

    // ── Audio transcript answer ───────────────────────────────────────────────
    public async IAsyncEnumerable<string> Stream(string transcript, string jd, string resume,
        IEnumerable<ConversationTurn>? history = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = new List<object>();
        foreach (var turn in (history ?? []).TakeLast(6))
        {
            messages.Add(new { role = "user",      content = new[] { new { text = turn.Question } } });
            messages.Add(new { role = "assistant", content = new[] { new { text = turn.Answer } } });
        }
        messages.Add(new { role = "user", content = new[] { new { text = $"Interview question:\n{transcript}" } } });

        var result = await ConverseAsync(SystemPrompt(jd, resume), messages, ct);
        yield return result;
    }

    // ── Screenshot / vision analysis ──────────────────────────────────────────
    public async IAsyncEnumerable<string> StreamVision(byte[] imageBytes, string jd, string resume,
        IEnumerable<ConversationTurn>? history = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = new List<object>();
        foreach (var turn in (history ?? []).TakeLast(4))
        {
            messages.Add(new { role = "user",      content = new[] { new { text = turn.Question } } });
            messages.Add(new { role = "assistant", content = new[] { new { text = turn.Answer } } });
        }

        var promptText = $"""
Analyze this screenshot carefully.

If it contains a **coding problem or algorithm question**:
- Provide a complete, correct solution
- Use proper markdown code blocks with the language tag
- Add brief inline comments for clarity
- Then give a short time/space complexity note

If it contains a **system design or conceptual question**:
- Give a structured markdown answer with headings and bullets

If it contains a **multiple choice or quiz question**:
- State the correct answer clearly, then explain why

Otherwise describe what you see and how to answer it.
{(string.IsNullOrWhiteSpace(jd)     ? "" : $"\n\nJob Description context:\n{jd}")}
{(string.IsNullOrWhiteSpace(resume) ? "" : $"\n\nCandidate Resume:\n{resume}")}
""";

        var base64 = Convert.ToBase64String(imageBytes);
        messages.Add(new
        {
            role = "user",
            content = new object[]
            {
                new { image = new { format = "jpeg", source = new { bytes = base64 } } },
                new { text = promptText }
            }
        });

        var systemMsg = "You are ShadowPilot, a Principal Engineer with 15+ years of experience. Analyze the screenshot and answer as the candidate (first person). Respond in clean markdown.";
        var result = await ConverseAsync(systemMsg, messages, ct);
        yield return result;
    }

    // ── Core Converse API call ────────────────────────────────────────────────
    private async Task<string> ConverseAsync(string system, List<object> messages, CancellationToken ct)
    {
        var url  = $"https://bedrock-runtime.{_region}.amazonaws.com/model/{_modelId}/converse";
        var body = JsonSerializer.Serialize(new
        {
            system = new[] { new { text = system } },
            messages,
            inferenceConfig = new { maxTokens = 1500, temperature = 0.7 }
        });

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-amzn-api-key", _apiKey);

        using var resp = await _http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Bedrock error {(int)resp.StatusCode}: {errBody}");
        }

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("output")
            .GetProperty("message")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("Empty response from Bedrock");
    }

    private static string SystemPrompt(string jd, string resume)
    {
        var s = """
You are ShadowPilot, an invisible interview co-pilot acting as a Principal Engineer with 15+ years of experience based in the United States.

ALWAYS answer as if YOU are the candidate — first person, confident, authoritative.
Use natural American English — contractions (I've, we'd, that's), American idioms, and a casual-professional tone like a senior engineer at a top US tech company.
Avoid British spellings or overly formal phrasing. Sound like a native American speaker.

Draw directly from the resume and job description provided. If resume mentions specific technologies, projects, or achievements, weave them naturally into answers.

Answer style:
- Think and respond like a Principal Engineer: system-level thinking, trade-offs, impact at scale
- Lead with the most impressive/relevant point first
- Use concise markdown bullets — readable at a glance during a live call
- For technical questions: give the correct answer first, then brief explanation
- For behavioral (STAR): Situation (1 line) → Action (2-3 lines) → Result (metric if possible)
- Never say "I think" or "maybe" — speak with conviction
- Keep total response under 150 words unless it's a coding question
""";
        if (!string.IsNullOrWhiteSpace(jd))     s += $"\n\nJob Description:\n{jd}";
        if (!string.IsNullOrWhiteSpace(resume)) s += $"\n\nResume:\n{resume}";
        return s;
    }
}
