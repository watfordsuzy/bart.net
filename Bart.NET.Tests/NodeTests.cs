using System.ComponentModel.Design;

namespace Bart.NET.Tests;

public class NodeTests
{
    [Fact]
    public void TestPrefixInsert()
    {
        List<SlowNTEntry<int>> pfxs = Shuffle(SlowNT.AllPrefixes()).GetRange(0, 100);
        SlowNT<int> slow = new(pfxs);
        Node<int> fast = new();

        foreach (SlowNTEntry<int> pfx in pfxs)
        {
            fast.InsertPrefix(pfx.Octet, pfx.Bits, pfx.Value);
        }

        for (int i = 0; i < 256; i++)
        {
            byte octet = (byte)i;
            (int slowVal, bool slowOK) = slow.Lpm(octet);
            (_, int fastVal, bool fastOK) = fast.LpmByOctet(octet);

            Assert.Equal(slowOK, fastOK);
            Assert.Equal(slowVal, fastVal);
        }
    }

    [Fact]
    public void TestPrefixDelete()
    {
        List<SlowNTEntry<int>> pfxs = Shuffle(SlowNT.AllPrefixes()).GetRange(0, 100);
        SlowNT<int> slow = new(pfxs);
        Node<int> fast = new();

        foreach (SlowNTEntry<int> pfx in pfxs)
        {
            fast.InsertPrefix(pfx.Octet, pfx.Bits, pfx.Value);
        }

        List<SlowNTEntry<int>> toDelete = pfxs.GetRange(0, 50);
        foreach (SlowNTEntry<int> pfx in toDelete)
        {
            slow.Delete(pfx.Octet, pfx.Bits);
            fast.RemovePrefix(pfx.Octet, pfx.Bits);
        }

        // Sanity check that slowTable seems to have done the right thing.
        Assert.Equal(50, slow.Entries.Count);

        for (int i = 0; i < 256; i++)
        {
            byte octet = (byte)i;
            (int slowVal, bool slowOK) = slow.Lpm(octet);
            (_, int fastVal, bool fastOK) = fast.LpmByOctet(octet);

            Assert.Equal(slowOK, fastOK);
            Assert.Equal(slowVal, fastVal);
        }
    }

    [Fact]
    public void TestPrefixOverlaps()
    {
        List<SlowNTEntry<int>> pfxs = Shuffle(SlowNT.AllPrefixes()).GetRange(0, 100);
        SlowNT<int> slow = new(pfxs);
        Node<int> fast = new();

        foreach (SlowNTEntry<int> pfx in pfxs)
        {
            fast.InsertPrefix(pfx.Octet, pfx.Bits, pfx.Value);
        }

        List<SlowNTEntry<int>> allPrefixes = SlowNT.AllPrefixes();
        foreach (SlowNTEntry<int> tt in allPrefixes)
        {
            bool slowOK = slow.OverlapsPrefix(tt.Octet, tt.Bits);
            bool fastOK = fast.OverlapsPrefix(tt.Octet, tt.Bits);
            Assert.Equal($"{tt.Octet} {slowOK}", $"{tt.Octet} {fastOK}");
        }
    }

    [Fact]
    public void TestNodeOverlaps()
    {
        // Empirically, between 5 and 6 routes per table results in ~50%
        // of random pairs overlapping. Cool example of the birthday paradox!
        const int numEntries = 6;
        List<SlowNTEntry<int>> all = SlowNT.AllPrefixes();

        Dictionary<bool, int> seenResult = new()
        {
            [false] = 0,
            [true] = 0,
        };
        for (int i = 0; i < 100_000; i++)
        {
            all = Shuffle(all);

            List<SlowNTEntry<int>> pfxs = all.GetRange(0, numEntries);

            SlowNT<int> slow = new(pfxs);
            Node<int> fast = new();

            foreach (SlowNTEntry<int> pfx in pfxs)
            {
                fast.InsertPrefix(pfx.Octet, pfx.Bits, pfx.Value);
            }

            List<SlowNTEntry<int>> inter = all.GetRange(numEntries, 2*numEntries);
            SlowNT<int> slowInter = new(inter);
            Node<int> fastInter = new();
            foreach (SlowNTEntry<int> pfx in inter)
            {
                fastInter.InsertPrefix(pfx.Octet, pfx.Bits, pfx.Value);
            }

            bool gotSlow = slow.Overlaps(slowInter);
            bool gotFast = fast.Overlaps(fastInter);
            Assert.Equal(gotSlow, gotFast);

            seenResult[gotFast]++;
        }

        // saw both intersections and non-intersections
        Assert.All(seenResult.Values, v => Assert.NotEqual(0, v));
    }
}
