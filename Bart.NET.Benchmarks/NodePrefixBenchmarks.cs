namespace Bart.NET.Benchmarks;

[MemoryDiagnoser]
public class NodePrefixBenchmarks
{
    private Node<int> _node = null!;
    private readonly List<SlowNTEntry<int>> _routes;

    [Params(10, 20, 50, 100, 200, 500)]
    public int PrefixCount { get; set; }

    public NodePrefixBenchmarks()
    {
        _routes = Shuffle(SlowNT.AllPrefixes());
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _node = new();
        for (int i = 0; i < Math.Min(this.PrefixCount, _routes.Count); ++i)
        {
            SlowNTEntry<int> route = _routes[i];
			_node.InsertPrefix(route.Octet, route.Bits, 0);
        }
    }

    [Benchmark]
    public void InsertPrefix()
    {
        SlowNTEntry<int> route = _routes[Random.Shared.Next(_routes.Count)];
        _node.InsertPrefix(route.Octet, route.Bits, 0);
    }

    [Benchmark]
    public void AddOrUpdatePrefix()
    {
        SlowNTEntry<int> route = _routes[Random.Shared.Next(_routes.Count)];
        _node.AddOrUpdatePrefix(route.Octet, route.Bits, _ => 0, (_, old) => old + 1);
    }

    [Benchmark]
    public bool RemovePrefix()
    {
        SlowNTEntry<int> route = _routes[Random.Shared.Next(_routes.Count)];
        return _node.RemovePrefix(route.Octet, route.Bits);
    }

    [Benchmark]
    public (uint baseIndex, int? val, bool ok) LpmByIndex()
    {
        SlowNTEntry<int> route = _routes[Random.Shared.Next(_routes.Count)];
        uint index = PrefixToBaseIndex(route.Octet, route.Bits);
        return _node.LpmByIndex(index);
    }
}
