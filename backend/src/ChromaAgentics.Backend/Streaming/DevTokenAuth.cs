using System.Security.Cryptography;
using System.Text;
using ChromaAgentics.Backend.Configuration;

namespace ChromaAgentics.Backend.Streaming;

public static class DevTokenAuth
{
    public const string HeaderName = "X-Chroma-Dev-Token";
    public const string QueryParameterName = "devToken";

    public static bool IsAuthorized(HttpContext context, BackendOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DevAuthToken))
        {
            return false;
        }

        var providedToken = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            providedToken = context.Request.Query[QueryParameterName].FirstOrDefault();
        }

        return FixedTimeEquals(providedToken, options.DevAuthToken);
    }

    private static bool FixedTimeEquals(string? providedToken, string expectedToken)
    {
        if (string.IsNullOrEmpty(providedToken))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
