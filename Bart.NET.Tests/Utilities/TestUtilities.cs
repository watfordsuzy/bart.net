using System.Net;

namespace Bart.NET.Tests.Utilities;

public static class TestUtilities
{
#if DEBUG
    private static Random RANDOM { get; } = new(1);
#else
    private static Random RANDOM => Random.Shared;
#endif

    public static List<T> Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = RANDOM.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
        return list;
    }

    public static IPAddress RandomAddr()
    {
        return RANDOM.Next(2) == 1
            ? RandomAddr6()
            : RandomAddr4();
    }

    public static IPAddress RandomAddr4()
    {
        byte[] bytes = new byte[4];
        RANDOM.NextBytes(bytes);
        return new IPAddress(bytes);
    }

    public static IPAddress RandomAddr6()
    {
        byte[] bytes = new byte[16];
        RANDOM.NextBytes(bytes);
        return new IPAddress(bytes);
    }

    private static void ApplyMask(byte[] bytes, int maskBits)
    {
        for (int i = 0; i < bytes.Length; i++, maskBits -= StrideLength)
        {
            if (maskBits <= 0)
            {
                bytes[i] = 0;
            }
            else
            {
                bytes[i] = FirstOctetOfPrefix(bytes[i], maskBits);
            }
        }
    }

    public static IPNetwork RandomPrefix4()
    {
        int bits = RANDOM.Next(1, 33);  // IPv4 ranges from 1 to 32

        byte[] bytes = new byte[4];
        RANDOM.NextBytes(bytes);

        ApplyMask(bytes, bits);

        return new(new(bytes), bits);
    }

    public static IPNetwork RandomPrefix6()
    {
        int bits = RANDOM.Next(1, 129);  // IPv6 ranges from 1 to 128

        byte[] bytes = new byte[16];
        RANDOM.NextBytes(bytes);

        ApplyMask(bytes, bits);

        return new(new(bytes), bits);
    }

    public static bool Overlaps(this IPNetwork a, IPNetwork b)
    {
        if (a.Equals(b))
        {
            return true;
        }

        // Check address family
        if (a.BaseAddress.AddressFamily != b.BaseAddress.AddressFamily)
        {
            return false;
        }

        int minPrefixLength = Math.Min(a.PrefixLength, b.PrefixLength);
        if (minPrefixLength == 0)
        {
            // default route
            return true;
        }

        try
        {
            // Adjust each IPNetwork to the minimum prefix length ...
            IPNetwork a0 = new(a.BaseAddress, minPrefixLength);
            IPNetwork b0 = new(b.BaseAddress, minPrefixLength);

            // ... and compare their addresses.
            return a0.BaseAddress == b0.BaseAddress;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
