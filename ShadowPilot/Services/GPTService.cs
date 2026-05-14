using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ShadowPilot.Models;

namespace ShadowPilot.Services;

public class GPTService
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };

    public GPTService(string apiKey, string baseUrl = "https://api.openai.com/v1", string model = "gpt-4o")
    {
        _apiKey  = apiKey;
        _baseUrl = baseUrl;
        _model   = model;
    }

    public IAsyncEnumerable<string> Stream(string transcript, string jd, string resume,
        IEnumerable<ConversationTurn>? history = null)
    {
        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt(jd, resume) }
        };

        foreach (var turn in (history ?? []).TakeLast(6))
        {
            messages.Add(new { role = "user",      content = turn.Question });
            messages.Add(new { role = "assistant", content = turn.Answer });
        }
        messages.Add(new { role = "user", content = $"Interview question:\n{transcript}" });

        return StreamMessages(messages);
    }

    public IAsyncEnumerable<string> StreamVision(byte[] imageBytes, string jd, string resume,
        IEnumerable<ConversationTurn>? history = null)
    {
        var base64 = Convert.ToBase64String(imageBytes);

        var messages = new List<object>
        {
            new { role = "system", content = """
You are ShadowPilot, a Principal Engineer with 15+ years of experience. Analyze the screenshot and answer as the candidate (first person).
Use the resume and job description context to make answers personal and specific.
Respond in clean markdown. For code problems: correct solution first with fenced code blocks, then O(n) complexity note.
Speak with the confidence of someone who has solved this problem before.
""" }
        };

        foreach (var turn in (history ?? []).TakeLast(4))
        {
            messages.Add(new { role = "user",      content = turn.Question });
            messages.Add(new { role = "assistant", content = turn.Answer });
        }

        var promptText = $"""
Analyze this screenshot carefully.

If it contains a **coding problem or algorithm question**:
- Provide a complete, correct solution
- Use proper markdown code blocks with the language tag (e.g. ```python)
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

        messages.Add(new
        {
            role = "user",
            content = new object[]
            {
                new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64}", detail = "high" } },
                new { type = "text", text = promptText }
            }
        });

        return StreamMessages(messages);
    }

    private async IAsyncEnumerable<string> StreamMessages(List<object> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            model    = _model,
            stream   = true,
            messages = messages,
        });

        var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {_apiKey}");

        using var resp   = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ") || line == "data: [DONE]") continue;

            var json = line[6..];
            try
            {
                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta")
                    .GetProperty("content")
                    .GetString();
                if (!string.IsNullOrEmpty(text)) yield return text;
            }
            catch { /* skip malformed chunks */ }
        }
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
