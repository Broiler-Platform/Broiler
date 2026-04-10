using System.Net;

namespace Broiler.LogAnalyzer;

/// <summary>
/// Provides IP-to-country resolution for geographic traffic analysis.
/// Ships with a built-in provider that classifies private/reserved IP ranges;
/// users can supply their own <see cref="ILookupProvider"/> (e.g. MaxMind) for
/// real geolocation data.
/// </summary>
public sealed class GeoIpService
{
    /// <summary>
    /// Interface for pluggable IP geolocation providers.
    /// </summary>
    public interface ILookupProvider
    {
        /// <summary>
        /// Returns the country name/code for the given IP address, or <c>null</c>
        /// if the address cannot be resolved.
        /// </summary>
        string? LookupCountry(string ipAddress);
    }

    /// <summary>
    /// Built-in provider that classifies private/reserved IP ranges per RFC 1918,
    /// RFC 5735, and related standards. Returns <c>null</c> for public addresses
    /// that it cannot classify.
    /// </summary>
    public sealed class BuiltInProvider : ILookupProvider
    {
        /// <inheritdoc/>
        public string? LookupCountry(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return null;

            if (!IPAddress.TryParse(ipAddress, out var ip))
                return null;

            byte[] bytes = ip.GetAddressBytes();

            // IPv4 checks
            if (bytes.Length == 4)
            {
                byte first = bytes[0];
                byte second = bytes[1];

                // Loopback: 127.0.0.0/8
                if (first == 127)
                    return "Private/Reserved";

                // 10.0.0.0/8 (RFC 1918)
                if (first == 10)
                    return "Private/Reserved";

                // 172.16.0.0/12 (RFC 1918)
                if (first == 172 && second >= 16 && second <= 31)
                    return "Private/Reserved";

                // 192.168.0.0/16 (RFC 1918)
                if (first == 192 && second == 168)
                    return "Private/Reserved";

                // Link-local: 169.254.0.0/16
                if (first == 169 && second == 254)
                    return "Private/Reserved";

                // 0.0.0.0/8 – current network
                if (first == 0)
                    return "Private/Reserved";

                // 100.64.0.0/10 – shared address space (CGN, RFC 6598)
                if (first == 100 && second >= 64 && second <= 127)
                    return "Private/Reserved";

                // 192.0.0.0/24 – IETF protocol assignments
                if (first == 192 && second == 0 && bytes[2] == 0)
                    return "Private/Reserved";

                // 192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24 – documentation
                if (first == 192 && second == 0 && bytes[2] == 2)
                    return "Private/Reserved";
                if (first == 198 && second == 51 && bytes[2] == 100)
                    return "Private/Reserved";
                if (first == 203 && second == 0 && bytes[2] == 113)
                    return "Private/Reserved";

                // 198.18.0.0/15 – benchmarking
                if (first == 198 && (second == 18 || second == 19))
                    return "Private/Reserved";

                // 224.0.0.0/4 – multicast
                if (first >= 224 && first <= 239)
                    return "Private/Reserved";

                // 240.0.0.0/4 – reserved for future use
                if (first >= 240)
                    return "Private/Reserved";

                return null;
            }

            // IPv6 checks
            if (bytes.Length == 16)
            {
                // ::1 loopback
                if (IPAddress.IsLoopback(ip))
                    return "Private/Reserved";

                // fc00::/7 – unique local addresses
                if ((bytes[0] & 0xFE) == 0xFC)
                    return "Private/Reserved";

                // fe80::/10 – link-local
                if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
                    return "Private/Reserved";

                // :: unspecified
                if (ip.Equals(IPAddress.IPv6None))
                    return "Private/Reserved";

                return null;
            }

            return null;
        }
    }

    private readonly ILookupProvider _provider;

    /// <summary>
    /// Creates a new <see cref="GeoIpService"/> using the supplied provider.
    /// Falls back to <see cref="BuiltInProvider"/> when no provider is given.
    /// </summary>
    public GeoIpService(ILookupProvider? provider = null)
    {
        _provider = provider ?? new BuiltInProvider();
    }

    /// <summary>
    /// Returns the country for the given IP address.
    /// Returns <c>"Unknown"</c> when the provider cannot resolve the address.
    /// </summary>
    public string LookupCountry(string ipAddress)
    {
        return _provider.LookupCountry(ipAddress) ?? "Unknown";
    }
}
