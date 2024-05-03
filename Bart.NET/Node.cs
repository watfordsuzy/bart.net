// Copyright (c) 2024 Suzy Inc.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Bart.NET;

/// <summary>
/// Represents a level node in a multibit-trie.
/// </summary>
/// <remarks>
/// A <see cref="Node{TValue}"/> has route prefixes and children. The route prefixes
/// form a complete binary tree, see the ART paper to understand the data structure.
/// In contrast to the ART algorithm, popcount-compressed lists are used
/// instead of fixed-size arrays. The list slots are also not pre-allocated as in
/// the ART algorithm, but backtracking is used for the longest-prefix-match.
/// The lookup is then slower by a factor of about 2, but this is the intended trade-off
/// to prevent memory consumption from exploding.
/// </remarks>
/// <typeparam name="TValue">The type of the values in the routing table.</typeparam>
internal class Node<TValue>
    where TValue : notnull
{
    private readonly BitSet _prefixesBitset = new();
    private readonly BitSet _childrenBitset = new();

    private readonly List<TValue> _prefixes = [];
    private readonly List<Node<TValue>> _children = [];

    /// <summary>
    /// Gets a value indicating whether or not the <see cref="Node{TValue}"/> is empty.
    /// </summary>
    public bool IsEmpty => _prefixes.Count == 0 && _children.Count == 0;

    /// <summary>
    /// Gets a value indicating whether or not the <see cref="Node{TValue}"/> has route
    /// prefixes.
    /// </summary>
    public bool HasPrefixes => _prefixes.Count != 0;

    /// <summary>
    /// Gets a value indicating whether or not the <see cref="Node{TValue}"/> has
    /// child nodes.
    /// </summary>
    public bool HasChildren => _children.Count != 0;

    /// <summary>
    /// Gets the key of the Popcount compression algorithm,
    /// mapping between a bitset index and the <c>_prefixes</c> list index.
    /// </summary>
    /// <param name="baseIndex">The bitset index.</param>
    /// <returns>An index into the <c>_prefixes</c> list.</returns>
    private int PrefixRank(uint baseIndex)
        => (int)_prefixesBitset.Rank(baseIndex) - 1;

    /// <summary>
    /// Adds the route described by <paramref name="octet"/> and
    /// <paramref name="prefixLength"/>, with the value in <paramref name="value"/>.
    /// </summary>
    /// <param name="octet">An octet of an IP network route.</param>
    /// <param name="prefixLength">The length of the CIDR prefix covering this octet.</param>
    /// <param name="value">The value associated with this route prefix.</param>
    public void InsertPrefix(byte octet, int prefixLength, TValue value)
    {
        this.InsertIndex(PrefixToBaseIndex(octet, prefixLength), value);
    }

    private void InsertIndex(uint baseIndex, TValue value)
    {
        if (_prefixesBitset.Contains(baseIndex))
        {
            _prefixes[this.PrefixRank(baseIndex)] = value;
        }
        else
        {
            _prefixesBitset.Set(baseIndex);
            _prefixes.Insert(this.PrefixRank(baseIndex), value);
        }
    }

    /// <summary>
    /// Removes a route described by <paramref name="octet"/> and
    /// <paramref name="prefixLength"/>.
    /// </summary>
    /// <param name="octet">An octet of an IP network route.</param>
    /// <param name="prefixLength">The length of the CIDR prefix covering this octet.</param>
    /// <returns><see langword="true"/> if the route prefix is successfully
    /// found and removed; otherwise <see langword="false"/>. This method
    /// returns <see langword="false"/> if the route prefix is not found
    /// in the <see cref="Node{TValue}"/>.</returns>
    public bool RemovePrefix(byte octet, int prefixLength)
    {
        uint baseIndex = PrefixToBaseIndex(octet, prefixLength);

        // no route entry
        if (!_prefixesBitset.Contains(baseIndex))
        {
            return false;
        }

        int rank = this.PrefixRank(baseIndex);
        _prefixes.RemoveAt(rank);

        _prefixesBitset.Clear(baseIndex);
        _prefixesBitset.Compact();

        return true;
    }

    /// <summary>
    /// Adds a route prefix described by <paramref name="octet"/> and
    /// <paramref name="prefixLength"/> if it does not already exist,
    /// or updates a route prefix if it already exists.
    /// </summary>
    /// <param name="octet">An octet of an IP network route.</param>
    /// <param name="prefixLength">The length of the CIDR prefix covering this octet.</param>
    /// <param name="addValueFactory">The function used to generate a value for an absent route.</param>
    /// <param name="updateValueFactory">The function used to generate a new value for an existing
    /// route based on the route's existing value.</param>
    /// <returns>The new value for the route prefix. This will either be the result of
    /// <paramref name="addValueFactory"/> (if the route prefix was absent) or the result
    /// of <paramref name="updateValueFactory"/> (if the route prefix was present).</returns>
    public TValue AddOrUpdatePrefix(
        byte octet, int prefixLength,
        Func<(byte octet, int prefixLength), TValue> addValueFactory,
        Func<(byte octet, int prefixLength), TValue, TValue> updateValueFactory)
    {
        uint baseIndex = PrefixToBaseIndex(octet, prefixLength);

        TValue value;
        if (_prefixesBitset.Contains(baseIndex))
        {
            // if prefix is set, get current value
            int rank = this.PrefixRank(baseIndex);
            value = _prefixes[rank];

            // callback function to get updated value
            value = updateValueFactory((octet, prefixLength), value);

            // prefix is already set, update and return value
            _prefixes[rank] = value;
        }
        else
        {
            // callback function to get new value
            value = addValueFactory((octet, prefixLength));

            // new prefix, insert into bitset ...
            _prefixesBitset.Set(baseIndex);

            // bitset has changed, recalc rank
            int rank = this.PrefixRank(baseIndex);

            // ... and insert value into prefixes
            _prefixes.Insert(rank, value);
        }

        return value;
    }

    /// <summary>
    /// Performs a longest-prefix-match by bitset index.
    /// </summary>
    /// <param name="index">The bitset index of the route prefix.</param>
    /// <returns>A tuple indicating whether or not a match was found,
    /// the bitset index of the match, and the value at the match (if found).</returns>
    public (uint baseIndex, TValue? value, bool ok) LpmByIndex(uint index)
    {
        // Maximum steps in backtracking is the stride length (assumed to be defined elsewhere)
        while (true)
        {
            if (_prefixesBitset.Contains(index))
            {
                // Longest prefix match found
                return (index, _prefixes[PrefixRank(index)], true);
            }

            if (index == 0)
            {
                break;
            }

            // Cache friendly backtracking to the next less specific route
            // Thanks to the complete binary tree it's just a shift operation
            index >>= 1;
        }

        // Not found (on this level)
        return (0, default, false);
    }

    /// <summary>
    /// Performs a longest-prefix-match by octet, adapts <see cref="LpmByIndex"/>.
    /// </summary>
    /// <param name="octet">The octet of the route prefix.</param>
    /// <returns>A tuple indicating whether or not a match was found,
    /// the bitset index of the match, and the value at the match (if found).</returns>
    public (uint baseIndex, TValue? value, bool ok) LpmByOctet(byte octet)
    {
        return this.LpmByIndex(OctetToBaseIndex(octet));
    }

    /// <summary>
    /// Performs a longest-prefix-match by route prefix, adapts <see cref="LpmByIndex"/>.
    /// </summary>
    /// <param name="octet">The octet of the route prefix.</param>
    /// <param name="prefixLength">The length of the CIDR prefix covering <paramref name="octet"/>.</param>
    /// <returns>A tuple indicating whether or not a match was found,
    /// the bitset index of the match, and the value at the match (if found).</returns>
    public (uint baseIndex, TValue? value, bool ok) LpmByPrefix(byte octet, int prefixLength)
    {
        return this.LpmByIndex(PrefixToBaseIndex(octet, prefixLength));
    }

    /// <summary>
    /// Determines if this <see cref="Node{TValue}"/> overlaps with a given
    /// <paramref name="octet"/> and <paramref name="prefixLength"/>.
    /// </summary>
    /// <param name="octet">The octet of the route prefix.</param>
    /// <param name="prefixLength">The length of the CIDR prefix covering <paramref name="octet"/>.</param>
    /// <returns><see langword="true"/> if this <see cref="Node{TValue}"/> contains
    /// the route prefix given by <paramref name="octet"/> and <paramref name="prefixLength"/>;
    /// otherwise <see langword="false"/>.</returns>
    public bool OverlapsPrefix(byte octet, int prefixLength)
    {
        // ##################################################
        // 1. test if any route in this node overlaps prefix?
        uint prefixIndex = PrefixToBaseIndex(octet, prefixLength);
        if (this.LpmByIndex(prefixIndex) is { ok: true })
        {
            return true;
        }

        // #################################################
        // 2. test if prefix overlaps any route in this node

        // lower/upper boundary for octet/pfxLen host routes
        uint prefixLowerBound = (uint)octet + FirstHostIndex;
        uint prefixUpperBound = LastHostIndexOfPrefix(octet, prefixLength);

        // increment to 'next' routeIdx for start in bitset search
        // since prefixIndex already tested by lpm in other direction
        uint routeIndex = prefixIndex << 1;
        while (_prefixesBitset.TryGetNextSet(routeIndex, out uint? nextRouteIndex))
        {
            routeIndex = nextRouteIndex.Value;

            (uint routeLowerBound, uint routeUpperBound) = LowerUpperBound(routeIndex);
            if (routeLowerBound >= prefixLowerBound && routeUpperBound <= prefixUpperBound)
            {
                return true;
            }

            // next route
            routeIndex++;
        }

        // #################################################
        // 3. test if prefix overlaps any child in this node

        // set start octet in bitset search with prefix octet
        uint childOctet = octet;
        while (_childrenBitset.TryGetNextSet(childOctet, out uint? nextChildOctet))
        {
            childOctet = nextChildOctet.Value;

            uint childIdx = childOctet + FirstHostIndex;
            if (childIdx >= prefixLowerBound && childIdx <= prefixUpperBound)
            {
                return true;
            }

            // next round
            childOctet++;
        }

        return false;
    }

    /// <summary>
    /// Gets the key of the Popcount compression algorithm,
    /// mapping between an octet and the <c>_children</c> list index.
    /// </summary>
    /// <param name="octet">The octet of the child node.</param>
    /// <returns>An index into the <c>_children</c> list.</returns>
    private int ChildRank(byte octet)
        => (int)_childrenBitset.Rank(octet) - 1;

    /// <summary>
    /// Inserts a child node at the given <paramref name="octet"/>.
    /// </summary>
    /// <param name="octet">The octet of the child node.</param>
    /// <param name="child">The child <see cref="Node{TValue}"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is <see langword="null"/>.</exception>
    public void InsertChild(byte octet, Node<TValue> child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_childrenBitset.Contains(octet))
        {
            _children[this.ChildRank(octet)] = child;
        }
        else
        {
            _childrenBitset.Set(octet);
            _children.Insert(this.ChildRank(octet), child);
        }
    }

    /// <summary>
    /// Removes a child <see cref="Node{TValue}"/> described by <paramref name="octet"/>.
    /// </summary>
    /// <param name="octet">An octet of an IP network route containing the child.</param>
    /// <returns><see langword="true"/> if the child <see cref="Node{TValue}"/> is successfully
    /// found and removed; otherwise <see langword="false"/>. This method
    /// returns <see langword="false"/> if the octet is not found
    /// in the <see cref="Node{TValue}"/>.</returns>
    public bool RemoveChild(byte octet)
    {
        if (!_childrenBitset.Contains(octet))
        {
            return false;
        }

        int rank = this.ChildRank(octet);
        _children.RemoveAt(rank);

        _childrenBitset.Clear(octet);
        _childrenBitset.Compact();

        return true;
    }

    /// <summary>
    /// Gets the value associated with the specified <paramref name="octet">.
    /// </summary>
    /// <param name="octet">An octet of an IP network route containing the child.</param>
    /// <param name="value">When this method returns, contains the child <see cref="Node{TValue}">
    /// associated with the specified octet, if the octet is found; otherwise, <see langword="null"/>.
    /// This parameter is passed uninitialized.</param>
    /// <returns><see langword="true"/> if <paramref name="octet"/> was found with a child
    /// in the <see cref="Node{TValue}"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetChild(byte octet, [NotNullWhen(true)] out Node<TValue>? child)
    {
        child = null;
        if (!_childrenBitset.Contains(octet))
        {
            return false;
        }

        child = _children[this.ChildRank(octet)];
        return true;
    }

    /// <summary>
    /// Gets a value indicating whether or not any IP in the other
    /// node overlaps with this node. First this tests the routes,
    /// then the children, and if no match in this node a recursive
    /// descent is performed through the child nodes with the same
    /// octet.
    /// </summary>
    /// <param name="other">Another route node.</param>
    /// <returns><see langword="true"> if and only if at least one
    /// IP overlaps with <paramref name="other"/>, otherwise
    /// <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool Overlaps(Node<TValue> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // dynamically allot the host routes from prefixes
        bool[] nAllotIndex = new bool[MaxNodePrefixes];
        bool[] oAllotIndex = new bool[MaxNodePrefixes];

        // 1. test if any routes overlaps?

        bool nOk = this.HasPrefixes;
        bool oOk = other.HasPrefixes;
        uint nIdx = 0, oIdx = 0;

        // zig-zag, for all routes in both nodes ...
        while (true)
        {
            if (nOk)
            {
                // range over bitset, node n
                if (_prefixesBitset.TryGetNextSet(nIdx, out uint? nextIndex))
                {
                    nOk = true;
                    nIdx = nextIndex.Value;

                    // get range of host routes for this prefix
                    (uint lowerBound, uint upperBound) = LowerUpperBound(nIdx);

                    // insert host routes (octet/8) for this prefix,
                    // some sort of allotment
                    for (uint i = lowerBound; i <= upperBound; i++)
                    {
                        // zig-zag, fast return
                        if (oAllotIndex[i])
                        {
                            return true;
                        }

                        nAllotIndex[i] = true;
                    }

                    nIdx++;
                }
                else
                {
                    nOk = false;
                }
            }

            if (oOk)
            {
                // range over bitset, node o
                if (other._prefixesBitset.TryGetNextSet(oIdx, out uint? nextIndex))
                {
                    oOk = true;
                    oIdx = nextIndex.Value;

                    // get range of host routes for this prefix
                    (uint lowerBound, uint upperBound) = LowerUpperBound(oIdx);

                    // insert host routes (octet/8) for this prefix,
                    // some sort of allotment
                    for (uint i = lowerBound; i <= upperBound; i++)
                    {
                        // zig-zag, fast return
                        if (nAllotIndex[i])
                        {
                            return true;
                        }

                        oAllotIndex[i] = true;
                    }
                    oIdx++;
                }
                else
                {
                    oOk = false;
                }
            }

            if (!nOk && !oOk)
            {
                break;
            }
        }

	    // full run, zig-zag didn't already match
        if (this.HasPrefixes && other.HasPrefixes)
        {
            for (int i = FirstHostIndex; i <= LastHostIndex; i++)
            {
                if (nAllotIndex[i] && oAllotIndex[i])
                {
                    return true;
                }
            }
        }

        // 2. test if routes overlaps any child

        bool[] nOctets = new bool[MaxNodeChildren];
        bool[] oOctets = new bool[MaxNodeChildren];

        nOk = _children.Count != 0;
        oOk = other._children.Count != 0;
        uint nOctet = 0, oOctet = 0;

        // zig-zag, for all octets in both nodes ...
        while (true)
        {
		    // range over bitset, node n
            if (nOk)
            {
                if (_childrenBitset.TryGetNextSet(nOctet, out uint? nextOctet))
                {
                    nOk = true;
                    nOctet = nextOctet.Value;

                    if (oAllotIndex[nOctet + FirstHostIndex])
                    {
                        return true;
                    }

                    nOctets[nOctet] = true;
                    nOctet++;
                }
                else
                {
                    nOk = false;
                }
            }

		    // range over bitset, node o
            if (oOk)
            {
                if (other._childrenBitset.TryGetNextSet(oOctet, out uint? nextOctet))
                {
                    oOk = true;
                    oOctet = nextOctet.Value;

                    if (nAllotIndex[oOctet + FirstHostIndex])
                    {
                        return true;
                    }

                    oOctets[oOctet] = true;
                    oOctet++;
                }
                else
                {
                    oOk = false;
                }
            }

            if (!nOk && !oOk)
            {
                break;
            }
        }

	    // 3. recursive descent call for childs with same octet

        if (this.HasChildren && other.HasChildren)
        {
            for (int i = 0; i < nOctets.Length; ++i)
            {
                if (nOctets[i] && oOctets[i])
                {
				    // get next child node for this octet
                    _ = TryGetChild((byte)i, out Node<TValue>? nc);
                    _ = other.TryGetChild((byte)i, out Node<TValue>? oc);

                    // recursive descent
                    if (nc is not null
                     && oc is not null
                     && nc.Overlaps(oc))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
