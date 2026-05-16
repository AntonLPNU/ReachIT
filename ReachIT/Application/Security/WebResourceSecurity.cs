// Central validation for project web resources before storing or opening links.
using System.Net;

namespace ReachIT.Application.Security;

public static class WebResourceSecurity
{
    private const int MaxUrlLength = 4096;

    public static string NormalizeAndValidateUrl(string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("URL is empty.");
        }

        if (value.Length > MaxUrlLength)
        {
            throw new InvalidOperationException("URL is too long.");
        }

        if (value.Any(char.IsControl) || value.Contains('\\'))
        {
            throw new InvalidOperationException("URL contains unsafe characters.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("URL must be absolute.");
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only http and https links are allowed for web resources.");
        }

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new InvalidOperationException("URLs with embedded credentials are not allowed.");
        }

        var host = NormalizeAndValidateHost(uri.Host);
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = host
        };

        if ((builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443)
            || (builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80))
        {
            builder.Port = -1;
        }

        return builder.Uri.AbsoluteUri;
    }

    public static List<string> NormalizeAndValidateUrls(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeAndValidateUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string NormalizeAndValidateHost(string value)
    {
        value = value.Trim().TrimEnd('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Host is empty.");
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only http and https hosts are allowed.");
            }

            value = uri.Host.ToLowerInvariant();
        }

        if (value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..];
        }

        if (value.Any(char.IsControl) || value.Contains('/') || value.Contains('\\') || value.Contains('@'))
        {
            throw new InvalidOperationException("Host contains unsafe characters.");
        }

        if (IsBlockedHost(value))
        {
            throw new InvalidOperationException("Local and private network hosts are not allowed in web resources.");
        }

        return value;
    }

    public static List<string> NormalizeAndValidateHosts(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeAndValidateHost)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsSafeWebUrl(string value)
    {
        try
        {
            _ = NormalizeAndValidateUrl(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBlockedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".home", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IsBlockedAddress(address);
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 0
                   || bytes[0] == 10
                   || bytes[0] == 127
                   || (bytes[0] == 169 && bytes[1] == 254)
                   || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168);
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal
                   || address.IsIPv6SiteLocal
                   || bytes[0] == 0xFC
                   || bytes[0] == 0xFD
                   || address.Equals(IPAddress.IPv6None)
                   || address.Equals(IPAddress.IPv6Any);
        }

        return true;
    }
}
