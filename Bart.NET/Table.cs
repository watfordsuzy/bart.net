// Copyright (c) 2024 Suzy Inc.
// SPDX-License-Identifier: MIT

using System.Net;

namespace Bart.NET;

public class Table<TValue>
{
    private readonly Node<TValue> _rootV4 = new();
    private readonly Node<TValue> _rootV6 = new();

    protected Node<TValue> GetRootByVersion(IPAddress ip)
        => ip.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => _rootV4,
            System.Net.Sockets.AddressFamily.InterNetworkV6 => _rootV6,
            _ => throw new ArgumentException($"Unsupported Address Family: {ip.AddressFamily}", nameof(ip)),
        };

    private static void ValidateIPAddressAndPrefixLength(IPAddress ip, int prefixLength)
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
                if (ip.IsIPv4MappedToIPv6)
                {
                    throw new ArgumentException("Convert IPv4-in-IPv6 mapped addresses to IPv4 first");
                }
                break;
            default:
                throw new ArgumentException($"Unsupported Address Family: {ip.AddressFamily}", nameof(ip));
        }
    }

    public void Insert(IPNetwork ipnw, TValue value)
        => Insert(ipnw.BaseAddress, ipnw.PrefixLength, value);

    public void Insert(IPAddress ip, int prefixLength, TValue value)
    {
        ValidateIPAddressAndPrefixLength(ip, prefixLength);

        // Get the root node of the routing table
        Node<TValue> node = this.GetRootByVersion(ip);

        // Insert default route, easy peasy
        if (prefixLength == 0)
        {
            node.InsertPrefix(0, 0, value);
            return;
        }

        Span<byte> octets = ip.AddressFamily switch {
            System.Net.Sockets.AddressFamily.InterNetworkV6 => stackalloc byte[16],
            _ => stackalloc byte[4],
        };

        if (!ip.TryWriteBytes(octets, out int bytesWritten)
            || bytesWritten != octets.Length)
        {
            octets = ip.GetAddressBytes();
        }

        foreach (byte octet in octets)
        {
            // Loop stop condition: last significant octet reached
            if (prefixLength <= StrideLength)
            {
                node.InsertPrefix(octet, prefixLength, value);
                return;
            }

            // Descend down to next trie level
            if (!node.TryGetChild(octet, out Node<TValue>? child))
            {
                // Create and insert missing intermediate child, no path compression!
                child = new Node<TValue>();
                node.InsertChild(octet, child);
            }

            // Go down
            node = child;
            prefixLength -= StrideLength;
        }
    }

    public bool TryGetValue(IPAddress ip, out TValue? value)
    {
        ArgumentNullException.ThrowIfNull(ip);

        value = default;

        Node<TValue> node = this.GetRootByVersion(ip);

        Span<byte> octets = ip.AddressFamily switch {
            System.Net.Sockets.AddressFamily.InterNetworkV6 => stackalloc byte[16],
            _ => stackalloc byte[4],
        };

        if (!ip.TryWriteBytes(octets, out int bytesWritten)
            || bytesWritten != octets.Length)
        {
            octets = ip.GetAddressBytes();
        }

        // stack of the traversed nodes for fast backtracking, if needed
        Stack<Node<TValue>> pathStack = new(MaxTreeDepth);

	    // find leaf node
        byte depth = 0;
        byte octet = 0;
        for ( ; depth < octets.Length; ++depth)
        {
            octet = octets[depth];

		    // go down in tight loop to leaf node
            if (node.TryGetChild(octet, out Node<TValue>? child)
                && child != null)
            {
                // push current node on stack for fast backtracking
                pathStack.Push(node);

                node = child;
                continue;
            }

            break;
        }

        // start backtracking at leaf node in tight loop
        while (depth >= 0)
        {
		    // lookup only in nodes with prefixes, skip over intermediate nodes
            if (node.HasPrefixes
                && node.LpmByOctet(octet) is { ok: true, val: TValue foundValue })
            {
    			// longest prefix match
                value = foundValue;
                return true;
            }

		    // end condition, stack is exhausted
            if (depth == 0)
            {
                break;
            }

    		// go up, backtracking
            depth--;
            octet = octets[depth];
            node = pathStack.Pop();
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether any IP in the table matches
    /// a route in the other table.
    /// </summary>
    /// <param name="other">Another routing table.</param>
    /// <returns><see langword="true"/> if and only if at least one IP
    /// in this table matches a route in the other table,
    /// otherwise <see langword="false"/>.</returns>
    public bool Overlaps(Table<TValue> other)
        => _rootV4.Overlaps(other._rootV4)
        || _rootV6.Overlaps(other._rootV6);
}
