using System.Net;

namespace Bart.NET.Tests;

public class TableTests
{
    [Fact]
    public void TestInsert()
    {
        Table<int> tbl = new();

        // Create a new leaf strideTable, with compressed path
        tbl.Insert(IPNetwork.Parse("192.168.0.1/32"), 1);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", false),
            new("192.168.0.3", false),
            new("192.168.0.255", false),
            new("192.168.1.1", false),
            new("192.170.1.1", false),
            new("192.180.0.1", false),
            new("192.180.3.5", false),
            new("10.0.0.5", false),
            new("10.0.0.15", false),
        ]);

        // Insert into previous leaf, no tree changes
        tbl.Insert(IPNetwork.Parse("192.168.0.2/32"), 2);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", false),
            new("192.168.0.255", false),
            new("192.168.1.1", false),
            new("192.170.1.1", false),
            new("192.180.0.1", false),
            new("192.180.3.5", false),
            new("10.0.0.5", false),
            new("10.0.0.15", false),
        ]);

        // Insert into previous leaf, unaligned prefix covering the /32s
        tbl.Insert(IPNetwork.Parse("192.168.0.0/26"), 7);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", true, 7),
            new("192.168.0.255", false),
            new("192.168.1.1", false),
            new("192.170.1.1", false),
            new("192.180.0.1", false),
            new("192.180.3.5", false),
            new("10.0.0.5", false),
            new("10.0.0.15", false),
        ]);

        // Create a different leaf elsewhere
        tbl.Insert(IPNetwork.Parse("10.0.0.0/27"), 3);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", true, 7),
            new("192.168.0.255", false),
            new("192.168.1.1", false),
            new("192.170.1.1", false),
            new("192.180.0.1", false),
            new("192.180.3.5", false),
            new("10.0.0.5", true, 3),
            new("10.0.0.15", true, 3),
        ]);

        // Insert that creates a new intermediate table and a new child
        tbl.Insert(IPNetwork.Parse("192.168.1.1/32"), 4);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", true, 7),
            new("192.168.0.255", false),
            new("192.168.1.1", true, 4),
            new("192.170.1.1", false),
            new("192.180.0.1", false),
            new("192.180.3.5", false),
            new("10.0.0.5", true, 3),
            new("10.0.0.15", true, 3),
        ]);

        // Insert that creates a new intermediate table but no new child
        tbl.Insert(IPNetwork.Parse("192.170.0.0/16"), 5);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", true, 7),
            new("192.168.0.255", false),
            new("192.168.1.1", true, 4),
            new("192.170.1.1", true, 5),
            new("192.180.0.1", false),
            new("192.180.3.5", false),
            new("10.0.0.5", true, 3),
            new("10.0.0.15", true, 3),
        ]);

        // New leaf in a different subtree, so the next insert can test a
        // variant of decompression.
        tbl.Insert(IPNetwork.Parse("192.180.0.1/32"), 8);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", true, 7),
            new("192.168.0.255", false),
            new("192.168.1.1", true, 4),
            new("192.170.1.1", true, 5),
            new("192.180.0.1", true, 8),
            new("192.180.3.5", false),
            new("10.0.0.5", true, 3),
            new("10.0.0.15", true, 3),
        ]);

        // Insert that creates a new intermediate table but no new child,
        // with an unaligned intermediate
        tbl.Insert(IPNetwork.Parse("192.180.0.0/21"), 9);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", true, 7),
            new("192.168.0.255", false),
            new("192.168.1.1", true, 4),
            new("192.170.1.1", true, 5),
            new("192.180.0.1", true, 8),
            new("192.180.3.5", true, 9),
            new("10.0.0.5", true, 3),
            new("10.0.0.15", true, 3),
        ]);

        // Insert a default route, those have their own codepath.
        tbl.Insert(IPNetwork.Parse("0.0.0.0/0"), 6);
        CheckRoutes(tbl, [
            new("192.168.0.1", true, 1),
            new("192.168.0.2", true, 2),
            new("192.168.0.3", true, 7),
            new("192.168.0.255", true, 6),
            new("192.168.1.1", true, 4),
            new("192.170.1.1", true, 5),
            new("192.180.0.1", true, 8),
            new("192.180.3.5", true, 9),
            new("10.0.0.5", true, 3),
            new("10.0.0.15", true, 3),
        ]);

        // Now all of the above again, but for IPv6.

        // Create a new leaf strideTable, with compressed path
        tbl.Insert(IPNetwork.Parse("ff:aaaa::1/128"), 1);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", false),
            new("ff:aaaa::3", false),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", false),
            new("ff:aaaa:aaaa:bbbb::1", false),
            new("ff:cccc::1", false),
            new("ff:cccc::ff", false),
            new("ffff:bbbb::5", false),
            new("ffff:bbbb::15", false),
        ]);

        // Insert into previous leaf, no tree changes
        tbl.Insert(IPNetwork.Parse("ff:aaaa::2/128"), 2);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", false),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", false),
            new("ff:aaaa:aaaa:bbbb::1", false),
            new("ff:cccc::1", false),
            new("ff:cccc::ff", false),
            new("ffff:bbbb::5", false),
            new("ffff:bbbb::15", false),
        ]);

        // Insert into previous leaf, unaligned prefix covering the /128s
        tbl.Insert(IPNetwork.Parse("ff:aaaa::/125"), 7);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", true, 7),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", false),
            new("ff:aaaa:aaaa:bbbb::1", false),
            new("ff:cccc::1", false),
            new("ff:cccc::ff", false),
            new("ffff:bbbb::5", false),
            new("ffff:bbbb::15", false),
        ]);

        // Create a different leaf elsewhere
        tbl.Insert(IPNetwork.Parse("ffff:bbbb::/120"), 3);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", true, 7),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", false),
            new("ff:aaaa:aaaa:bbbb::1", false),
            new("ff:cccc::1", false),
            new("ff:cccc::ff", false),
            new("ffff:bbbb::5", true, 3),
            new("ffff:bbbb::15", true, 3),
        ]);

        // Insert that creates a new intermediate table and a new child
        tbl.Insert(IPNetwork.Parse("ff:aaaa:aaaa::1/128"), 4);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", true, 7),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", true, 4),
            new("ff:aaaa:aaaa:bbbb::1", false),
            new("ff:cccc::1", false),
            new("ff:cccc::ff", false),
            new("ffff:bbbb::5", true, 3),
            new("ffff:bbbb::15", true, 3),
        ]);

        // Insert that creates a new intermediate table but no new child
        tbl.Insert(IPNetwork.Parse("ff:aaaa:aaaa:bb00::/56"), 5);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", true, 7),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", true, 4),
            new("ff:aaaa:aaaa:bbbb::1", true, 5),
            new("ff:cccc::1", false),
            new("ff:cccc::ff", false),
            new("ffff:bbbb::5", true, 3),
            new("ffff:bbbb::15", true, 3),
        ]);

        // New leaf in a different subtree, so the next insert can test a
        // variant of decompression.
        tbl.Insert(IPNetwork.Parse("ff:cccc::1/128"), 8);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", true, 7),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", true, 4),
            new("ff:aaaa:aaaa:bbbb::1", true, 5),
            new("ff:cccc::1", true, 8),
            new("ff:cccc::ff", false),
            new("ffff:bbbb::5", true, 3),
            new("ffff:bbbb::15", true, 3),
        ]);

        // Insert that creates a new intermediate table but no new child,
        // with an unaligned intermediate
        tbl.Insert(IPNetwork.Parse("ff:cccc::/37"), 9);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", true, 7),
            new("ff:aaaa::255", false),
            new("ff:aaaa:aaaa::1", true, 4),
            new("ff:aaaa:aaaa:bbbb::1", true, 5),
            new("ff:cccc::1", true, 8),
            new("ff:cccc::ff", true, 9),
            new("ffff:bbbb::5", true, 3),
            new("ffff:bbbb::15", true, 3),
        ]);

        // Insert a default route, those have their own codepath.
        tbl.Insert(IPNetwork.Parse("::/0"), 6);
        CheckRoutes(tbl, [
            new("ff:aaaa::1", true, 1),
            new("ff:aaaa::2", true, 2),
            new("ff:aaaa::3", true, 7),
            new("ff:aaaa::255", true, 6),
            new("ff:aaaa:aaaa::1", true, 4),
            new("ff:aaaa:aaaa:bbbb::1", true, 5),
            new("ff:cccc::1", true, 8),
            new("ff:cccc::ff", true, 9),
            new("ffff:bbbb::5", true, 3),
            new("ffff:bbbb::15", true, 3),
        ]);
    }

    [Fact]
    public void TestInsertShuffled()
    {
        List<SlowRTEntry<int>> all = SlowRT.RandomPrefixes(1000);

        for (int i = 0; i < 10; i++)
        {
            List<SlowRTEntry<int>> prefixes = Shuffle([..all]);

            List<IPAddress> addrs = [];
            for (int j = 0; j < 10_000; j++)
            {
                addrs.Add(RandomAddr());
            }

            Table<int> t1 = new();
            Table<int> t2 = new();

            foreach (SlowRTEntry<int> prefix in all)
            {
                t1.Insert(prefix.Prefix, prefix.Value);
            }
            foreach (SlowRTEntry<int> prefix in prefixes)
            {
                t2.Insert(prefix.Prefix, prefix.Value);
            }

            foreach (IPAddress addr in addrs)
            {
                bool ok1 = t1.TryGetValue(addr, out int val1);
                bool ok2 = t2.TryGetValue(addr, out int val2);

                Assert.Equal(ok1, ok2);
                if (ok1)
                {
                    Assert.Equal(val1, val2);
                }
            }
        }
    }

    #region Tailscale ART tests

    // original comment by tailscale for ART,
    //
    // These tests are specific triggers for subtle correctness issues
    // that came up during initial implementation. Even if they seem
    // arbitrary, please do not clean them up. They are checking edge
    // cases that are very easy to get wrong, and quite difficult for
    // the other statistical tests to trigger promptly.
    //
    // ... but the BART implementation is different and has other edge cases.

    [Fact]
    public void TestRegression_prefixes_aligned_on_stride_boundary()
    {
		Table<int> fast = new();
		SlowRT<int> slow = new([]);

		fast.Insert(IPNetwork.Parse("226.205.197.0/24"), 1);
		slow.Insert(IPNetwork.Parse("226.205.197.0/24"), 1);

		fast.Insert(IPNetwork.Parse("226.205.0.0/16"), 2);
		slow.Insert(IPNetwork.Parse("226.205.0.0/16"), 2);

		IPAddress probe = IPAddress.Parse("226.205.121.152");
		bool gotOK = fast.TryGetValue(probe, out int got);
		(int want, bool wantOK) = slow.Lookup(probe);
        Assert.Equal(wantOK, gotOK);
        if (gotOK)
        {
            Assert.Equal(want, got);
        }
    }

	[Fact]
    public void TestRegression_parent_prefix_inserted_in_different_orders()
    {
		Table<int> t1 = new(), t2 = new();

		t1.Insert(IPNetwork.Parse("136.20.0.0/16"), 1);
		t1.Insert(IPNetwork.Parse("136.20.201.62/32"), 2);

		t2.Insert(IPNetwork.Parse("136.20.201.62/32"), 2);
		t2.Insert(IPNetwork.Parse("136.20.0.0/16"), 1);

		IPAddress a = IPAddress.Parse("136.20.54.139");
		bool ok1 = t1.TryGetValue(a, out int got1);
		bool ok2 = t2.TryGetValue(a, out int got2);

        Assert.Equal(ok1, ok2);
        if (ok1)
        {
            Assert.Equal(got1, got2);
        }
	}

    [Fact]
    public void TestRegression_overlaps_divergent_children_with_parent_route_entry()
    {
		Table<int> t1 = new(), t2 = new();

		t1.Insert(IPNetwork.Parse("128.0.0.0/2"), 1);
		t1.Insert(IPNetwork.Parse("99.173.128.0/17"), 1);
		t1.Insert(IPNetwork.Parse("219.150.142.0/23"), 1);
		t1.Insert(IPNetwork.Parse("164.148.190.250/31"), 1);
		t1.Insert(IPNetwork.Parse("48.136.229.233/32"), 1);

		t2.Insert(IPNetwork.Parse("217.32.0.0/11"), 1);
		t2.Insert(IPNetwork.Parse("38.176.0.0/12"), 1);
		t2.Insert(IPNetwork.Parse("106.16.0.0/13"), 1);
		t2.Insert(IPNetwork.Parse("164.85.192.0/23"), 1);
		t2.Insert(IPNetwork.Parse("225.71.164.112/31"), 1);

		Assert.True(t1.Overlaps(t2));
	}

	[Fact]
    public void TestRegression_overlaps_parent_child_comparison_with_route_in_parent()
    {
		Table<int> t1 = new(), t2 = new();

		t1.Insert(IPNetwork.Parse("226.0.0.0/8"), 1);
		t1.Insert(IPNetwork.Parse("81.128.0.0/9"), 1);
		t1.Insert(IPNetwork.Parse("152.0.0.0/9"), 1);
		t1.Insert(IPNetwork.Parse("151.220.0.0/16"), 1);
		t1.Insert(IPNetwork.Parse("89.162.61.0/24"), 1);

		t2.Insert(IPNetwork.Parse("54.0.0.0/9"), 1);
		t2.Insert(IPNetwork.Parse("35.89.128.0/19"), 1);
		t2.Insert(IPNetwork.Parse("72.33.53.0/24"), 1);
		t2.Insert(IPNetwork.Parse("2.233.60.32/27"), 1);
		t2.Insert(IPNetwork.Parse("152.42.142.160/28"), 1);

		Assert.True(t1.Overlaps(t2));
	}

#endregion Tailscale ART tests

    private static void CheckRoutes<TValue>(Table<TValue> table, params RouteTest<TValue>[] routes)
    {
        foreach (RouteTest<TValue> route in routes)
        {
            bool exists = table.TryGetValue(route.IP, out TValue? value);
            if (route.ShouldExist != exists)
            {
                throw new Xunit.Sdk.XunitException($"Route {route.IP} unexpectedly {(route.ShouldExist ? "does not exist" : "exists")} ({value})");
            }

            if (!EqualityComparer<TValue>.Default.Equals(route.ExpectedValue, value))
            {
                throw new Xunit.Sdk.AssertActualExpectedException(route.ExpectedValue, value, $"Route {route.IP} has the wrong value");
            }
        }
    }

    private record class RouteTest<TValue>(string ipAddress, bool ShouldExist, TValue? ExpectedValue = default)
    {
        public IPAddress IP { get; } = IPAddress.Parse(ipAddress);
    }
}
