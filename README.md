# CacheList

An immutable, persistent indexed sequence for F#, implemented as a wide B+ tree with private arrays. Targets .NET 10; the library has no dependencies beyond FSharp.Core.

```fsharp
open CacheCollections

let original = CacheList.ofList [1; 2; 3]
let edited = original |> CacheList.add 4 |> CacheList.updateAt 0 10
let joined = CacheList.append original edited
let removed = CacheList.removeAt 1 joined
let doubled = removed |> CacheList.map ((*) 2)
let total = doubled |> Seq.sum
// original still contains 1, 2, 3
```

Reference `src/CacheList/CacheList.fsproj` from an F# project. `CacheList<'T>` implements `IEnumerable<'T>` (F# `seq<'T>`), non-generic `IEnumerable`, and `IReadOnlyList<'T>`. It exposes `Length`, `Count`, `IsEmpty`, and an indexer. Equality, hashing, and lexicographic comparison use element values, independently of tree shape.

## Storage and persistence

Leaves store up to 32 adjacent values. Branches store up to 32 child references and cumulative element counts, allowing binary search by index. All leaves have the same depth. Non-root nodes remain between 16 and 32 entries through splitting, merging, and redistribution; the root can be smaller and collapses when it has one child. This is a B+ tree sequence, inspired by the proposed B* direction; it uses half-full occupancy rather than strict B* two-thirds occupancy.

Edits copy the affected paths and reuse untouched subtrees. Concatenation joins the boundary paths at their matching heights. Arrays are never mutated after publication and are never exposed. Input arrays are copied, and `toArray` returns a fresh array. Independent readers and forks can safely share a value without locks. Immutability is shallow, as with F# lists: mutable objects stored as elements are still mutable. User callbacks and externally changing input sequences remain the caller's responsibility.

With branching factor fixed at 32:

| Operation | Time | New storage |
| --- | --- | --- |
| Length / isEmpty | O(1) | O(1) |
| item / head / last | O(log n) | O(1) |
| add / prepend / insertAt / updateAt / removeAt / tail / removeLast | O(log n) | O(log n) |
| append two trees | O(log(n + m)) | O(log(n + m)) |
| ofArray / ofList / ofSeq | O(n) | O(n) |
| Enumeration / fold / iter | O(n) | Traversal state |
| map / filter / choose | O(n) | O(n) |

These bounds hide copying at most a small number of 32-entry arrays per level. Counts and indices are 32-bit; edits reject totals beyond `Int32.MaxValue`. `ofSeq` eagerly consumes its input. Bulk construction distributes each level evenly. It uses temporary arrays, so peak allocation exceeds final tree size. No constant-time prepend or tail guarantee is implied; a linked list remains a good choice for stack workloads.

## Module helpers

Functions follow List's curried argument order, with the collection last where List does so. `add value source` appends a single value; `append left right` concatenates collections. `prepend` and `cons` add at the front. Index errors raise `ArgumentException`; `tryItem`, `tryHead`, and `tryLast` return options. `tail` and `removeLast` reject empty collections.

The module includes construction and conversion; indexed edits; map/mapi/map2/mapi2/map3; collect/concat; filter/choose/partition; fold/foldBack/fold2/foldBack2; scan/scanBack/mapFold/mapFoldBack; iter/iteri/iter2/iteri2; searches in both directions; exists/forall and their two-input versions; numeric aggregates; sorting; distinct/except; zip/unzip variants; allPairs; slicing; grouping; windowing; chunking; transpose; permute; and unfold.

Module helpers operate directly on the tree. Length-preserving transforms such as `map`, `mapi`, `map2`, and `mapFold` retain its shape, allocate new value and child arrays, and share immutable cumulative counts. Filtering, choosing, partitioning, and other variable-length transforms build packed leaves without staging the whole result in a sequence buffer or linked list. Slicing shares covered subtrees and balances the boundaries; chunks and windows can also share storage with their input.

Folds, iteration, and searches traverse leaf arrays directly, with searches stopping at the first match in the requested direction. Multi-input operations traverse independent cursors so differently shaped trees work together. Sorting uses stable merges over private working arrays; grouping and distinct operations use structural hashing and retain first-occurrence order, including null keys. General `seq` enumeration uses one traversal stack per enumerator. The source is the authoritative public API.

## Build and test

```powershell
dotnet build CacheList.slnx -c Release
dotnet test CacheList.slnx -c Release
```

The xUnit suite checks array aliasing, concurrent forks, physical subtree sharing, old snapshots after randomized edits, model equivalence, concatenation across tree heights, leaf depth, occupancy, cumulative counts, root collapse, helper behavior against List, and invalid arguments. Module regression tests also cover shape-preserving transforms, randomized slicing and bulk edits, callback order and short-circuiting, stable sorting, structural/null keys, differently shaped inputs, overflow, and sequence disposal.

## Benchmarks

Run without a filter to select an operation interactively (enter its number, several numbers, or `*` for all):

```powershell
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --job short
```

Or select operations directly:

```powershell
# Append only (CacheList, Array, and List at both sizes):
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*Append*' --job short
# Prepend only:
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*Prepend*' --job short
# Both removal operations (or use '*RemoveMiddle*' / '*RemoveLast*' individually):
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*Remove*' --job short
# Multiple operations:
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*Append*' '*Prepend*' --job short
# Map and filter only (use '*Map.*' to exclude Mapi):
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*Map.*' '*Filter.*' --job short
# All module helper benchmarks:
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --anyCategories Module --filter '*' --job short
# All operations:
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*' --job short
# List available benchmarks without running them:
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --list flat
# Fast validation of every case:
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*' --job Dry
```

The independent groups are `Append`, `Prepend`, `RemoveMiddle`, `RemoveLast`, `Update`, `Index`, `Iterate`, `Build`, `InsertMiddle`, `Concat`, and `Sequence`. Each group produces its own report. Filters also support individual implementations, such as `'*TreeAppend*'`.

Module helper groups are `Map`, `Mapi`, `Filter`, `Choose`, `Partition`, `Reverse`, `SortBy`, `DistinctBy`, `GroupBy`, `CountBy`, `Scan`, and `SumBy`. They compare the corresponding CacheList, Array, and List functions and can be selected individually from the same menu or together with the `Module` category.

All helpers use the shared input containing integers from zero to N - 1. Map adds one; Mapi adds the index. Filter and Partition select even values (50%); Choose selects even values and adds one. SortBy sorts by the negated value, reversing the initially ascending input. DistinctBy, GroupBy, and CountBy use the value modulo 128, producing 128 keys. Scan and SumBy accumulate as `int64` to avoid integer overflow. Each benchmark returns the complete result in its collection's native representation, including nested groups, so allocation includes materialising those results. The existing Iterate group compares module folds.

BenchmarkDotNet measures time and managed allocation at 1,024 and 65,536 elements. Cases compare append, prepend, middle insertion and removal, last removal, replacement, indexing, fold traversal, sequence traversal, concatenation, and bulk construction against F# List and arrays. Array edits copy their input so that comparisons preserve previous versions. Every benchmark starts from a stable prebuilt input, returns the result, and does not grow shared state across invocations. Build cases measure construction from an existing array. Allocation includes a new result, not retained snapshots. Run the default isolated process toolchain for reliable comparisons; in-process short runs are useful for local feedback and have wider uncertainty.

Reports are written to `BenchmarkDotNet.Artifacts/results/`. See [the recorded local run](docs/benchmarks.md) for measured results and limitations, and [the module optimization comparison](docs/module-optimization.md) for before/after measurements.
