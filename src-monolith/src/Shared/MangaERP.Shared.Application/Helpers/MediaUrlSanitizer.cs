using System;

namespace MangaERP.Shared.Application.Helpers;

public static class MediaUrlSanitizer
{
    /// <summary>
    /// Sanitizes media URLs (cover images, manuscripts, artwork).
    /// If the URL is empty, whitespace, a dummy test string (e.g. "http://cover.jpg"), or an invalid non-HTTP URL,
    /// returns null so that clients can reliably use internal fallback artwork instead of attempting to load a broken link.
    /// </summary>
    public static string? Sanitize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();

        if (url.Equals("string", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("http://cover.jpg", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("https://cover.jpg", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("http://manuscript.pdf", StringComparison.OrdinalIgnoreCase) ||
            url.Equals("https://manuscript.pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (uri.Host.Equals("cover.jpg", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("manuscript.pdf", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("example.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return url;
    }
}
