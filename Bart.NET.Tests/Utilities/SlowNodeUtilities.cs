namespace Bart.NET.Tests.Utilities;

public class SlowNT<T>(List<SlowNTEntry<T>> entries)
{
    private readonly List<SlowNTEntry<T>> _entries = entries;

    public IReadOnlyList<SlowNTEntry<T>> Entries => _entries.AsReadOnly();

    public void Delete(byte octet, int prefixLen)
    {
        _entries.RemoveAll(e => e.Octet == octet && e.Bits == prefixLen);
    }

    public (T? value, bool found) Lpm(byte octet)
    {
        int longest = -1;
        T? ret = default;
        foreach (var entry in _entries)
        {
            if ((octet & PfxMask(entry.Bits)) == entry.Octet && entry.Bits > longest)
            {
                ret = entry.Value;
                longest = entry.Bits;
            }
        }
        return (ret, longest != -1);
    }

    public bool OverlapsPrefix(byte octet, int prefixLen)
    {
        foreach (var entry in _entries)
        {
            int minBits = Math.Min(prefixLen, entry.Bits);
            byte mask = (byte)~HostMask(minBits);
            if ((octet & mask) == (entry.Octet & mask))
            {
                return true;
            }
        }
        return false;
    }

    public bool Overlaps(SlowNT<T> other)
    {
        foreach (var tp in _entries)
        {
            foreach (var op in other._entries)
            {
                int minBits = Math.Min(tp.Bits, op.Bits);
                if ((tp.Octet & PfxMask(minBits)) == (op.Octet & PfxMask(minBits)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static byte PfxMask(int pfxLen)
    {
        return (byte)(0xFF << (StrideLength - pfxLen));
    }
}

public class SlowNT
{
    public static List<SlowNTEntry<int>> AllPrefixes()
    {
        var ret = new List<SlowNTEntry<int>>();
        for (int idx = 1; idx < MaxNodePrefixes; idx++)
        {
            (byte octet, int bits) = BaseIndexToPrefix((uint)idx);
            ret.Add(new SlowNTEntry<int> { Octet = octet, Bits = bits, Value = idx });
        }
        return ret;
    }
}

public class SlowNTEntry<T>
{
    public byte Octet { get; set; }
    public int Bits { get; set; }
    public T Value { get; set; } = default!;
}
