namespace ShadowPilot.Services;

public static class EnvConfig
{
    public static string BedrockKey    => Load("BEDROCK_API_KEY");
    public static string BedrockRegion => Load("BEDROCK_REGION") is { Length: > 0 } r ? r : "us-east-1";
    public static string OpenRouterKey => Load("OPENROUTER_API_KEY");
    public static string OpenAIKey     => Load("OPENAI_API_KEY");

    private static string Load(string key)
    {
        // 1. Process environment variable
        var env = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var exeDir  = AppContext.BaseDirectory;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var candidates = new[]
        {
            Path.Combine(appData, ".shadowpilot.env"),
            Path.Combine(appData, ".env"),
            Path.Combine(exeDir,  ".env"),
            Path.Combine(exeDir,  "../.env"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            var contents = File.ReadAllText(path);
            var val = Parse(contents, key);
            if (!string.IsNullOrWhiteSpace(val)) return val;
        }

        return string.Empty;
    }

    private static string? Parse(string contents, string key)
    {
        foreach (var line in contents.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith('#') || !t.Contains('=')) continue;
            var idx = t.IndexOf('=');
            if (t[..idx].Trim() == key)
                return t[(idx + 1)..].Trim();
        }
        return null;
    }
}
