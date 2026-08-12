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

            // JWT payloads are base64url: map the alphabet back before padding,
            // or any payload containing '-' or '_' (non-ASCII username) fails to
            // decode and expiry is silently lost.
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
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

    public static long? SecondsUntilExpiry(string token)
    {
        var exp = GetExpiration(token);
        if (exp == null) return null;
        return (long)(exp.Value - DateTimeOffset.UtcNow).TotalSeconds;
    }
}
