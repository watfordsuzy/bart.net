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

```csharp
/// <summary>
/// Represents a routing table which can store values for specific
/// route prefixes, and lookup values associated with matching IP addresses.
/// </summary>
public class Table<TValue>
{
    /// <summary>
    /// Inserts a route into the table with the given value.
    /// </summary>
    /// <param name="ipnw">The <see cref="IPNetwork"/> instance describing the route.</param>
    /// <param name="value">The value to associate with the route.</param>
    public void Insert(IPNetwork ipnw, TValue value) {}

    /// <summary>
    /// Inserts a route into the table with the given value.
    /// </summary>
    /// <param name="ip">The base address of the route.</param>
    /// <param name="prefixLength">The CIDR prefix length of the route.</param>
    /// <param name="value">The value to associate with the route.</param>
    public void Insert(IPAddress ip, int prefixLength, TValue value) {}

    /// <summary>
    /// Determines whether the <see cref="Table{TValue}"/> contains a route with the specified IP.
    /// </summary>
    /// <param name="ip">The <see cref="IPAddress"/> to locate in the <see cref="Table{TValue}"/>.</param>
    /// <returns><see langword="true"/> if the <see cref="Table{TValue}"/> contains a route
    /// which includes the specified IP; otherwise, <see langword="false"/>.</returns>
    public bool Contains(IPAddress ip) {}

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
    public bool TryGetValue(IPAddress ip, [NotNullWhen(true)] out TValue? value) {}

    /// <summary>
    /// Gets a value indicating whether any IP in the table matches
    /// a route in the other table.
    /// </summary>
    /// <param name="other">Another routing table.</param>
    /// <returns><see langword="true"/> if and only if at least one IP
    /// in this table matches a route in the other table,
    /// otherwise <see langword="false"/>.</returns>
    public bool Overlaps(Table<TValue> other) {}
}
```

## Benchmarks

```
BenchmarkDotNet v0.13.12, macOS Sonoma 14.4.1 (23E224) [Darwin 23.4.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 8.0.0 (8.0.23.53103), Arm64 RyuJIT AdvSIMD


| Method      | RouteCount | AddressFamily  | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------------ |----------- |--------------- |----------:|---------:|---------:|-------:|----------:|
| Insert      | 10         | InterNetwork   |  48.22 ns | 0.632 ns | 0.591 ns | 0.0051 |      32 B |
| TryGetValue | 10         | InterNetwork   |  39.68 ns | 0.686 ns | 0.642 ns | 0.0293 |     184 B |
| Insert      | 10         | InterNetworkV6 | 145.69 ns | 0.856 ns | 0.800 ns | 0.0050 |      32 B |
| TryGetValue | 10         | InterNetworkV6 |  36.82 ns | 0.745 ns | 0.697 ns | 0.0293 |     184 B |
| Insert      | 100        | InterNetwork   |  47.97 ns | 0.684 ns | 0.640 ns | 0.0051 |      32 B |
| TryGetValue | 100        | InterNetwork   |  44.51 ns | 0.181 ns | 0.161 ns | 0.0293 |     184 B |
| Insert      | 100        | InterNetworkV6 | 162.25 ns | 0.642 ns | 0.569 ns | 0.0050 |      32 B |
| TryGetValue | 100        | InterNetworkV6 |  55.30 ns | 0.951 ns | 0.843 ns | 0.0293 |     184 B |
| Insert      | 1000       | InterNetwork   |  46.81 ns | 0.196 ns | 0.164 ns | 0.0051 |      32 B |
| TryGetValue | 1000       | InterNetwork   |  66.72 ns | 0.344 ns | 0.305 ns | 0.0293 |     184 B |
| Insert      | 1000       | InterNetworkV6 | 158.66 ns | 2.317 ns | 2.167 ns | 0.0050 |      32 B |
| TryGetValue | 1000       | InterNetworkV6 |  68.54 ns | 0.260 ns | 0.243 ns | 0.0293 |     184 B |
| Insert      | 10000      | InterNetwork   |  47.32 ns | 0.168 ns | 0.157 ns | 0.0051 |      32 B |
| TryGetValue | 10000      | InterNetwork   |  68.80 ns | 1.417 ns | 1.325 ns | 0.0293 |     184 B |
| Insert      | 10000      | InterNetworkV6 | 159.53 ns | 1.015 ns | 0.900 ns | 0.0050 |      32 B |
| TryGetValue | 10000      | InterNetworkV6 |  77.05 ns | 0.388 ns | 0.344 ns | 0.0293 |     184 B |
| Insert      | 100000     | InterNetwork   |  48.71 ns | 0.821 ns | 0.768 ns | 0.0051 |      32 B |
| TryGetValue | 100000     | InterNetwork   |  82.91 ns | 0.459 ns | 0.429 ns | 0.0293 |     184 B |
| Insert      | 100000     | InterNetworkV6 | 195.27 ns | 3.770 ns | 3.703 ns | 0.0050 |      32 B |
| TryGetValue | 100000     | InterNetworkV6 |  94.92 ns | 0.801 ns | 0.710 ns | 0.0293 |     184 B |
```

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
