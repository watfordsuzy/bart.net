// Copyright (c) 2024 Suzy Inc.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Bart.NET;

public class Node<TValue>
{
    public readonly BitSet _prefixesBitset = new();
    private readonly BitSet _childrenBitset = new();

    public readonly List<TValue> _prefixes = [];
    private readonly List<Node<TValue>> _children = [];

    public bool IsEmpty => _prefixes.Count == 0 && _children.Count == 0;

    public bool HasPrefixes => _prefixes.Count != 0;

    public bool HasChildren => _children.Count != 0;

    public int PrefixRank(uint baseIndex)
        => (int)_prefixesBitset.Rank(baseIndex) - 1;

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

    public TValue AddOrUpdatePrefix(byte octet, int prefixLength, Func<TValue?, TValue> updater)
    {
        var baseIndex = PrefixToBaseIndex(octet, prefixLength);

        int? rank = null;
        TValue? value = default;
        if (_prefixesBitset.Contains(baseIndex))
        {
            // if prefix is set, get current value
            rank = PrefixRank(baseIndex);
            value = _prefixes[rank.Value];
        }

        // callback function to get updated or new value
        value = updater(value);

        if (rank.HasValue)
        {
            // prefix is already set, update and return value
            _prefixes[rank.Value] = value;
        }
        else
        {
            // new prefix, insert into bitset ...
            _ = _prefixesBitset.TrySet(baseIndex);

            // bitset has changed, recalc rank
            rank = PrefixRank(baseIndex);

            // ... and insert value into prefixes
            _prefixes.Insert(rank.Value, value);
        }

        return value;
    }

    // Longest Prefix Match by Index
    public (uint baseIdx, TValue? val, bool ok) LpmByIndex(uint idx)
    {
        // Maximum steps in backtracking is the stride length (assumed to be defined elsewhere)
        while (true)
        {
            if (_prefixesBitset.Contains(idx))
            {
                // Longest prefix match found
                return (idx, _prefixes[PrefixRank(idx)], true);
            }

            if (idx == 0)
            {
                break;
            }

            // Cache friendly backtracking to the next less specific route
            // Thanks to the complete binary tree it's just a shift operation
            idx >>= 1;
        }

        // Not found (on this level)
        return (0, default, false);
    }

    // LPM by Octet, adapter to LpmByIndex
    public (uint baseIdx, TValue? val, bool ok) LpmByOctet(byte octet)
    {
        return this.LpmByIndex(OctetToBaseIndex(octet));
    }

    // LPM by Prefix, adapter to LpmByIndex
    public (uint baseIdx, TValue? val, bool ok) LpmByPrefix(byte octet, int bits)
    {
        return this.LpmByIndex(PrefixToBaseIndex(octet, bits));
    }

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
        uint pfxLowerBound = (uint)octet + FirstHostIndex;
        uint pfxUpperBound = LastHostIndexOfPrefix(octet, prefixLength);

        // increment to 'next' routeIdx for start in bitset search
        // since prefixIndex already tested by lpm in other direction
        uint routeIdx = prefixIndex << 1;
        while (_prefixesBitset.TryGetNextSet(routeIdx, out uint? nextRouteIdx))
        {
            routeIdx = nextRouteIdx.Value;

            (uint routeLowerBound, uint routeUpperBound) = LowerUpperBound(routeIdx);
            if (routeLowerBound >= pfxLowerBound && routeUpperBound <= pfxUpperBound)
            {
                return true;
            }

            // next route
            routeIdx++;
        }

        // #################################################
        // 3. test if prefix overlaps any child in this node

        // set start octet in bitset search with prefix octet
        uint childOctet = octet;
        while (_childrenBitset.TryGetNextSet(childOctet, out uint? nextChildOctet))
        {
            childOctet = nextChildOctet.Value;

            uint childIdx = childOctet + FirstHostIndex;
            if (childIdx >= pfxLowerBound && childIdx <= pfxUpperBound)
            {
                return true;
            }

            // next round
            childOctet++;
        }

        return false;
    }

    public int ChildRank(byte octet)
        => (int)_childrenBitset.Rank(octet) - 1;

    public void InsertChild(byte octet, Node<TValue> child)
    {
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
    public bool Overlaps(Node<TValue> other)
    {
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
