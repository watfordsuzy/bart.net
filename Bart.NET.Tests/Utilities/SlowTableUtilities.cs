using System.Net;

namespace Bart.NET.Tests.Utilities;

public class SlowRT<T>(List<SlowRTEntry<T>> entries)
{
    private readonly List<SlowRTEntry<T>> _entries = entries;

    public void Insert(IPNetwork ipnw, T val)
        => this.Insert(ipnw.BaseAddress, ipnw.PrefixLength, val);

    public void Insert(IPAddress ip, int bits, T val)
    {
        var pfx = new IPNetwork(ip, bits); // Custom Prefix class needed
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Prefix.Equals(pfx))
            {
                _entries[i] = new SlowRTEntry<T> { Prefix = pfx, Value = val };
                return;
            }
        }
        _entries.Add(new SlowRTEntry<T> { Prefix = pfx, Value = val });
    }

    public void Union(SlowRT<T> other)
    {
        foreach (SlowRTEntry<T> op in other._entries)
        {
            bool match = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Prefix.Equals(op.Prefix))
                {
                    _entries[i] = op;
                    match = true;
                    break;
                }
            }
            if (!match)
            {
                _entries.Add(op);
            }
        }
    }

    public (T? val, bool ok) Lookup(IPAddress addr)
    {
        int bestLen = -1;
        T? val = default;
        bool ok = false;

        foreach (SlowRTEntry<T> item in _entries)
        {
            if (item.Prefix.Contains(addr) && item.Prefix.PrefixLength > bestLen)
            {
                val = item.Value;
                ok = true;
                bestLen = item.Prefix.PrefixLength;
            }
        }
        return (val, ok);
    }

    public (T? val, bool ok) LookupPrefix(IPAddress ip, int bits)
    {
        IPNetwork pfx = new(ip, bits);
        int bestLen = -1;
        T? val = default;
        bool ok = false;

        foreach (SlowRTEntry<T> item in _entries)
        {
            if (item.Prefix.Overlaps(pfx)
             && item.Prefix.PrefixLength <= bits
             && item.Prefix.PrefixLength > bestLen)
            {
                val = item.Value;
                ok = true;
                bestLen = item.Prefix.PrefixLength;
            }
        }

        return (val, ok);
    }
}

public class SlowRT
{
    public static List<SlowRTEntry<int>> RandomPrefixes(int n)
    {
        List<SlowRTEntry<int>> prefixes = RandomPrefixes4(n / 2);
        prefixes.AddRange(RandomPrefixes6(n - prefixes.Count));
        return prefixes;
    }

    public static List<SlowRTEntry<int>> RandomPrefixes4(int n)
    {
        HashSet<IPNetwork> prefixes = [];
        while (prefixes.Count < n)
        {
            prefixes.Add(RandomPrefix4());
        }

        List<SlowRTEntry<int>> entries = [];
        foreach (IPNetwork prefix in prefixes)
        {
            entries.Add(new SlowRTEntry<int> { Prefix = prefix, Value = Random.Shared.Next() });
        }

        return entries;
    }

    public static List<SlowRTEntry<int>> RandomPrefixes6(int n)
    {
        HashSet<IPNetwork> prefixes = [];
        while (prefixes.Count < n)
        {
            prefixes.Add(RandomPrefix6());
        }

        List<SlowRTEntry<int>> entries = [];
        foreach (IPNetwork prefix in prefixes)
        {
            entries.Add(new SlowRTEntry<int> { Prefix = prefix, Value = Random.Shared.Next() });
        }

        return entries;
    }
}

public class SlowRTEntry<T>
{
    public IPNetwork Prefix { get; set; }

    public T Value { get; set; } = default!;
}
