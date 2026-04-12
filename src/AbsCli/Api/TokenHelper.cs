using System.Text.Json;

namespace AbsCli.Api;

public static class TokenHelper
{
    public static DateTimeOffset? GetExpiration(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1];
            // Fix base64 padding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                var expUnix = expElement.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(expUnix);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsExpiringSoon(string token, int thresholdSeconds = 60)
    {
        var exp = GetExpiration(token);
        if (exp == null) return false;

        return exp.Value <= DateTimeOffset.UtcNow.AddSeconds(thresholdSeconds);
    }
}
