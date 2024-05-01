namespace Bart.NET.Benchmarks;

[MemoryDiagnoser]
public class NodeChildBenchmarks
{
    private readonly Node<int> _child = new();
    private Node<int> _node = null!;

    [Params(10, 20, 50, 100, 200, 250)]
    public int ChildCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _node = new();
        for (int i = 0; i < this.ChildCount; ++i)
        {
            byte octet = (byte)Random.Shared.Next(MaxNodeChildren);
			_node.InsertChild(octet, _child);
        }
    }

    [Benchmark]
    public void InsertChild()
    {
        byte octet = (byte)Random.Shared.Next(MaxNodeChildren);
        _node.InsertChild(octet, _child);
    }

    [Benchmark]
    public bool RemoveChild()
    {
        byte octet = (byte)Random.Shared.Next(MaxNodeChildren);
        return _node.RemoveChild(octet);
    }

    [Benchmark]
    public bool TryGetChild()
    {
        byte octet = (byte)Random.Shared.Next(MaxNodeChildren);
        return _node.TryGetChild(octet, out _);
    }
}
