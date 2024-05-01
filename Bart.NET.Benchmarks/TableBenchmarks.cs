using System.Net;
using System.Net.Sockets;

namespace Bart.NET.Benchmarks;

[MemoryDiagnoser]
public class TableBenchmarks
{
    private Table<int> _table = null!;
    private IPAddress[] _addrs = [];
    private IPNetwork[] _probes = [];

    [Params(10, 100, 1000, 10_000, 100_000)]
    public int RouteCount { get; set; }

    [Params(AddressFamily.InterNetwork, AddressFamily.InterNetworkV6)]
    public AddressFamily AddressFamily { get; set; }

    private IPAddress GetRandomAddress()
        => this.AddressFamily == AddressFamily.InterNetwork
         ? RandomAddr4()
         : RandomAddr6();

    private IPNetwork GetRandomRoute()
        => this.AddressFamily == AddressFamily.InterNetwork
         ? RandomPrefix4()
         : RandomPrefix6();

    [GlobalSetup]
    public void GlobalSetup()
    {
        _table = new();
        for (int i = 0; i < this.RouteCount; ++i)
        {
            _table.Insert(GetRandomRoute(), i);
        }

        _addrs =[..Enumerable.Range(0, 128).Select(_ => GetRandomAddress())];
        _probes = [..Enumerable.Range(0, 128).Select(_ => GetRandomRoute())];
    }

    [Benchmark]
    public void Insert()
    {
        IPNetwork probe = _probes[Random.Shared.Next(_probes.Length)];
        _table.Insert(probe, -1);
    }

    [Benchmark]
    public bool TryGetValue()
    {
        IPAddress addr = _addrs[Random.Shared.Next(_addrs.Length)];
        return _table.TryGetValue(addr, out _);
    }
}
