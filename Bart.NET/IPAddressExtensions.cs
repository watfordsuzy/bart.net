using System.Net;

namespace Bart.NET;

/// <summary>
/// Provides extension methods for <see cref="IPAddress"/>.
/// </summary>
public static class IPAddressExtensions
{
    /// <summary>
    /// Converts an <see cref="IPAddress" /> and CIDR prefix length to a
    /// valid <see cref="IPNetwork"/> (i.e. masking).
    /// </summary>
    /// <param name="ip">An <see cref="IPAddress"/> representing the base
    /// part of an <see cref="IPNetwork"/>.</param>
    /// <param name="prefixLength">The CIDR prefix length.</param>
    /// <returns>An <see cref="IPNetwork"/> with the appropriate
    /// base address and prefix length.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ip"/> is <see langword="null"/>.</exception>
    public static IPNetwork ToIPNetwork(this IPAddress ip, int prefixLength)
    {
        ArgumentNullException.ThrowIfNull(ip);
        switch (ip.AddressFamily)
        {
            case System.Net.Sockets.AddressFamily.InterNetwork:
                ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(prefixLength, 32);
                break;
            case System.Net.Sockets.AddressFamily.InterNetworkV6:
                ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(prefixLength, 128);
                break;
            default:
                throw new ArgumentException($"Unsupported Address Family: {ip.AddressFamily}", nameof(ip));
        }

        Span<byte> octets = ip.AddressFamily switch {
            System.Net.Sockets.AddressFamily.InterNetworkV6 => stackalloc byte[16],
            System.Net.Sockets.AddressFamily.InterNetwork => stackalloc byte[4],
            _ => [],
        };

        if (!ip.TryWriteBytes(octets, out int bytesWritten)
            || bytesWritten != octets.Length)
        {
            throw new ArgumentException("Unsupported octet count for IP address", nameof(ip));
        }

        int maskBits = prefixLength;
        for (int i = 0; i < octets.Length; i++, maskBits -= StrideLength)
        {
            if (maskBits <= 0)
            {
                octets[i] = 0;
            }
            else
            {
                octets[i] = FirstOctetOfPrefix(octets[i], maskBits);
            }
        }

        return new IPNetwork(new IPAddress(octets), prefixLength);
    }
}
