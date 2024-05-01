namespace Bart.NET.Tests;

public class BitSetTests
{
    [Fact]
    public void TestRank()
    {
        uint[] u = [2, 3, 5, 7, 11, 700, 1500];
        BitSet b = new();
        foreach (uint v in u) Assert.True(b.TrySet(v));

        Assert.Equal(3U, b.Rank(5));
        Assert.Equal(3U, b.Rank(6));
        Assert.Equal(7U, b.Rank(1500));
    }

    [Fact]
    public void TestPopcntSlice()
    {
        ulong[] s = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29];
        int res = BitSet.PopCount(s.AsSpan());
        Assert.Equal(27, res);
    }

    [Fact]
    public void TestNextSetError()
    {
        BitSet b = new();
        bool result = b.TryGetNextSet(1, out uint? index);
        Assert.False(result);
        Assert.Null(index);
    }

    [Fact]
    public void TestIterate()
    {
        BitSet v = new();
        v.Set(0);
        v.Set(1);
        v.Set(2);

        List<uint> data = [];

        bool ok = v.TryGetNextSet(0, out uint? nextIndex);
        while (ok)
        {
            data.Add(nextIndex!.Value);
            ok = v.TryGetNextSet(nextIndex.Value + 1, out nextIndex);
        }

        Assert.Equal(0U, data[0]);
        Assert.Equal(1U, data[1]);
        Assert.Equal(2U, data[2]);

        v.Set(10);
        v.Set(2000);
        data.Clear();

        ok = v.TryGetNextSet(0, out nextIndex);
        while (ok)
        {
            data.Add(nextIndex!.Value);
            ok = v.TryGetNextSet(nextIndex.Value + 1, out nextIndex);
        }

        Assert.Equal(0U, data[0]);
        Assert.Equal(1U, data[1]);
        Assert.Equal(2U, data[2]);
        Assert.Equal(10U, data[3]);
        Assert.Equal(2000U, data[4]);
    }
}
