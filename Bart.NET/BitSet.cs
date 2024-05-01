// Copyright (c) 2024 Suzy Inc.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace Bart.NET;

public class BitSet
{
    // the wordSize of a bit set
    const int wordSize = 64;

    // the wordSize of a bit set in bytes
    const int wordBytes = wordSize / 8;

    // log2WordSize is lg(wordSize)
    const int log2WordSize = 6;

    // allBits has every bit set
    const ulong allBits = 0xffffffffffffffff;

    const uint Capacity = ~0U;

	private uint _length = 0;
    private ulong[] _set = [0UL];

    public bool IsEmpty
    {
        get
        {
            if (_set is not null)
            {
                for (int i = 0; i < _set.Length; ++i)
                {
                    if (_set[i] > 0) return false;
                }
            }

            return true;
        }
    }

    public bool Contains(uint index)
    {
        if (index >= _length)
        {
            return false;
        }

        uint wordsIndex = WordsIndex(index);
        uint offset = index >> log2WordSize;

        ulong value = _set[offset] & (1UL << (int)wordsIndex);

        return value != 0;
    }

    /// <summary>
    /// Rank returns the nunber of set bits up to and including the index
    /// that are set in the bitset.
    /// See https://en.wikipedia.org/wiki/Ranking#Ranking_in_statistics
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public uint Rank(uint index)
    {
        if (index >= _length)
        {
            return this.PopCount();
        }

        uint leftOver = (index + 1) & 63U;
        int answer = PopCount(_set.AsSpan(0, (int)((index + 1) >> 6)));
        if (leftOver != 0)
        {
            answer += BitOperations.PopCount(_set[(index + 1) >> 6] << (int)(64 - leftOver));
        }

        return (uint)answer;
    }

    public bool TrySet(uint index)
    {
        if (index >= _length)
        {
            this.ExtendSet(index);
        }

        uint wordsIndex = WordsIndex(index);
        uint offset = index >> log2WordSize;

        ulong previous = _set[offset] & (1UL << (int)wordsIndex);
        _set[offset] |= 1UL << (int)wordsIndex;

        return previous == 0;
    }

    public void Set(uint index)
        => TrySet(index);

    private void ExtendSet(uint index)
    {
        if (index > Capacity)
        {
            throw new InvalidOperationException($"Cannot extend bitset beyond {Capacity}");
        }

        int size = WordsNeeded(index + 1);
        if (_set.Length < size)
        {
            ulong[] set = new ulong[size];
            Array.Copy(_set, set, _set.Length);
            _set = set;
        }

        _length = index + 1;
    }

    public void Clear(uint index)
    {
        if (index < _length)
        {
            _set[index >> log2WordSize] &= ~(1UL << (int)WordsIndex(index));
        }
    }

    public void Compact()
    {
        // TODO
    }

    // TryGetNextSet returns the next bit set from the specified index,
    // including possibly the current index
    // along with an error code (true = valid, false = no set bit found)
    // for i,e := v.NextSet(0); e; i,e = v.NextSet(i + 1) {...}
    //
    // Users concerned with performance may want to use NextSetMany to
    // retrieve several values at once.
    public bool TryGetNextSet(uint i, [NotNullWhen(true)] out uint? nextBit)
    {
        nextBit = null;

        int x = (int)(i >> log2WordSize);
        if (x >= _set.Length)
        {
            return false;
        }

        ulong w = _set[x] >> (int)WordsIndex(i);
        if (w != 0)
        {
            nextBit = i + (uint)BitOperations.TrailingZeroCount(w);
            return true;
        }

        x++;

        // bounds check elimination in the loop
        if (x < 0)
        {
            return false;
        }

        while (x < _set.Length)
        {
            if (_set[x] != 0)
            {
                nextBit = (uint)((x * wordSize) + BitOperations.TrailingZeroCount(_set[x]));
                return true;
            }

            x++;
        }

        return false;
    }

    public ulong[] ToArray()
        => [.._set];

    private static uint WordsIndex(uint index)
        => index & (wordSize - 1);

    private static int WordsNeeded(uint index)
    {
        if (index > (Capacity - wordSize + 1))
        {
            return (int)(Capacity >> log2WordSize);
        }

        return (int)((index + (wordSize - 1)) >> log2WordSize);
    }

    public uint PopCount()
        => (uint)PopCount(_set.AsSpan());

    internal static int PopCount(ReadOnlySpan<ulong> set)
    {
        int popcount = 0;
        for (int i = 0; i < set.Length; ++i)
        {
            popcount += BitOperations.PopCount(set[i]);
        }

        return popcount;
    }

    public string DumpAsBits()
    {
        if (_set.Length == 0) return ".";

        StringBuilder builder = new();
        for (int i = _set.Length - 1; i >= 0; --i)
        {
            builder.Append(_set[i].ToString("b64"));
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
