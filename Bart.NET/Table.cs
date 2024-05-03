// Copyright (c) 2024 Suzy Inc.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Bart.NET;

/// <summary>
/// Represents a routing table that can store values per route prefix (IPv4 and IPv6).
/// </summary>
/// <typeparam name="TValue">The type of the values in the routing table.</typeparam>
public sealed class Table<TValue>
    where TValue : notnull
{
    private readonly Node<TValue> _rootV4 = new();
    private readonly Node<TValue> _rootV6 = new();

    private Node<TValue> GetRootByVersion(IPAddress ip)
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
                if (ip.IsIPv4MappedToIPv6)
                {
                    throw new ArgumentException("Convert IPv4-in-IPv6 mapped addresses to IPv4 first");
                }
                ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(prefixLength, 128);
                break;
            default:
                throw new ArgumentException($"Unsupported Address Family: {ip.AddressFamily}", nameof(ip));
        }
    }

    /// <summary>
    /// Inserts a route into the table with the given value.
    /// </summary>
    /// <param name="ipnw">The <see cref="IPNetwork"/> instance describing the route.</param>
    /// <param name="value">The value to associate with the route.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ipnw"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><para><paramref name="ipnw"/> does not have a supported <see cref="IPAddress.AddressFamily"/></para>
    /// <para>-or-</para>
    /// <para><paramref name="ipnw"/> represents an IPv4 route mapped to an IPv6 address. Map the address into IPv4 space
    /// (<see cref="IPAddress.MapToIPv4"/>) and convert the <see cref="IPNetwork.PrefixLength"/> first.</para></exception>
    /// <exception cref="ArgumentOutOfRangeException">The <see cref="IPNetwork.PrefixLength"/> is not valid.</exception>
    public void Insert(IPNetwork ipnw, TValue value)
    {
        ArgumentNullException.ThrowIfNull(ipnw);

        this.Insert(ipnw.BaseAddress, ipnw.PrefixLength, value);
    }

    /// <summary>
    /// Inserts a route into the table with the given value.
    /// </summary>
    /// <param name="ip">The base address of the route.</param>
    /// <param name="prefixLength">The CIDR prefix length of the route.</param>
    /// <param name="value">The value to associate with the route.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ip"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><para><paramref name="ip"/> does not have a supported <see cref="IPAddress.AddressFamily"/></para>
    /// <para>-or-</para>
    /// <para><paramref name="ip"/> represents an IPv4 route mapped to an IPv6 address. Map the address into IPv4 space
    /// (<see cref="IPAddress.MapToIPv4"/>) and convert <paramref name="prefixLength"/> first.</para></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="prefixLength"/> is not valid for <paramref name="ip"/>.</exception>
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

    /// <summary>
    /// Determines whether the <see cref="Table{TValue}"/> contains a route with the specified IP.
    /// </summary>
    /// <param name="ip">The <see cref="IPAddress"/> to locate in the <see cref="Table{TValue}"/>.</param>
    /// <returns><see langword="true"/> if the <see cref="Table{TValue}"/> contains a route
    /// which includes the specified IP; otherwise, <see langword="false"/>.</returns>
    public bool Contains(IPAddress ip)
        => this.TryGetValue(ip, out _);

    /// <summary>
    /// Gets the value associated with the specified <see cref="IPAddress"/>.
    /// </summary>
    /// <param name="ip">The <see cref="IPAddress"/> to search within the routing table.</param>
    /// <param name="value">When this method returns, contains the value associated with
    /// the specified IP, if a route containing the IP is found; otherwise, the default
    /// value for the type of the <paramref name="value"/> parameter. This parameter
    /// is passed uninitialized.</param>
    /// <returns><see langword="true"/> if <paramref name="ip"/> was found within a route
    /// in the <see cref="Table{TValue}"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(IPAddress ip, [NotNullWhen(true)] out TValue? value)
    {
        ArgumentNullException.ThrowIfNull(ip);

        value = default;

        // Map IPv4-over-IPv6 back to IPv4
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

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
                && node.LpmByOctet(octet) is { ok: true, value: TValue foundValue })
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
