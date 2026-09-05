using System.Net;
using System.Net.Sockets;

namespace PeopleHQ.Infrastructure.Integrations;

/// <summary>
/// SSRF defense for tenant-supplied webhook URLs. A tenant admin (via WebhookWrite) can register any URL,
/// and WebhookDispatcher makes the server itself issue an HTTP request to it — without this guard, that's a
/// direct path to internal services, cloud metadata endpoints (169.254.169.254), or loopback-bound admin
/// interfaces. Two checkpoints use this: subscription creation (reject obviously-bad targets up front) and
/// WebhookDispatcher's connect-time callback (defends against DNS rebinding between creation and delivery,
/// or between one delivery and the next — a hostname's DNS record can legitimately change after the
/// creation-time check passes).
/// </summary>
public static class SsrfGuard
{
    public static bool IsAllowedTargetUrl(string url, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(parsed.UserInfo)) return false; // blocks the userinfo@host credential trick
        uri = parsed;
        return true;
    }

    public static async Task<bool> ResolvesToPublicAddressAsync(string host, CancellationToken ct)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (SocketException)
        {
            return false; // unresolvable — fail closed
        }
        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    public static bool IsPublicAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal) return false;
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return false;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return false;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return false;
            // 169.254.0.0/16 (link-local, includes the 169.254.169.254 cloud metadata endpoint)
            if (bytes[0] == 169 && bytes[1] == 254) return false;
            // 100.64.0.0/10 (carrier-grade NAT)
            if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) return false;
            // 0.0.0.0/8
            if (bytes[0] == 0) return false;
        }

        return true;
    }
}
