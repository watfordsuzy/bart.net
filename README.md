# Bart.NET

## Overview

Bart.NET provides a Balanced-Routing-Table (BART) for .NET 8.

Bart.NET is a port of the [bart Go package](https://github.com/gaissmai/bart).

BART is balanced in terms of memory consumption versus lookup time.

The lookup time is by a factor of ~2 slower on average as the
routing algorithms ART, SMART, CPE, ... but reduces the memory
consumption by an order of magnitude in comparison.

BART is a multibit-trie with fixed stride length of 8 bits,
using the _baseIndex_ function from the ART algorithm to
build the complete-binary-tree (CBT) of prefixes for each stride.

The second key factor is popcount array compression at each stride level
of the CBT prefix tree and backtracking along the CBT in O(k).

The CBT is implemented as a bitvector, backtracking is just
a matter of fast cache friendly bitmask operations.

The child array at each stride level is also popcount compressed.

## API

...

## Benchmarks

...

## CONTRIBUTION

Please open an issue for discussion before sending a pull request.

## CREDIT

Credit to Karl Gaissmaier for the wonderful bart Go package this
project is ported from. Also credit to Will Fitzgerald, et al, for the
bitset Go package this projected ported parts of to flesh out
missing primitives in the .NET ecosystem.

His original credits are below:

> Credits for many inspirations go to the clever guys at tailscale,
> to Daniel Lemire for the super-fast bitset package and
> to Donald E. Knuth for the **ART** routing algorithm and
> all the rest of his *Art* and for keeping important algorithms
> in the public domain!

## LICENSE

MIT
