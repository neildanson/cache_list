# CacheMap: a packed persistent ordered map

CacheMap applies the same principle as CacheList to key/value lookup: keep many entries in each private array and copy a short tree path for edits. It is an **ordered B+ tree**, rather than a hash dictionary. That preserves comparison-based key identity and sorted traversal, making F# Map a useful behavioral and performance reference. It lives in the existing `CacheCollections` assembly and adds no library dependencies.

```fsharp
open CacheCollections

let original = CacheMap.ofList ["Ada", 10; "Grace", 20]
let edited = original |> CacheMap.add "Ada" 11 |> CacheMap.add "Barbara" 30
let filtered = edited |> CacheMap.filter (fun _ score -> score >= 20)
let labels = edited |> CacheMap.map (fun name score -> $"{name}: {score}")
let score = CacheMap.tryFind "Ada" original // Some 10; original remains intact
```

## Representation and guarantees

Leaves hold parallel sorted key and value arrays. Branches hold child references, each child's greatest key, and a cached subtree count. Search uses binary search at each level, reading values only after finding a leaf entry. The parallel arrays allow `map` to share immutable keys and branch bounds while allocating only new values, child arrays, and node objects. Folds and searches visit the arrays directly. Enumeration uses one traversal stack, with ascending keys.

Leaves contain at most 32 entries and branches at most 32 children. Non-root nodes contain at least 16; all leaves have equal depth. Insertions split; deletions merge or redistribute with a neighbor. The root can be smaller and collapses when only one child remains. Removal of a missing key returns the source itself. An edit creates new arrays on the changed path and shares untouched subtrees. Constructors never retain the caller's arrays; conversions return fresh arrays.

32/16 is an initial compromise inherited from CacheList, **not a demonstrated optimum for maps**. Keys, comparisons, separate key/value arrays, and representative-key replacement change the costs. A 32-key node is not necessarily one cache line, managed allocations have no cache-line alignment promise, and siblings are separate managed objects. This is not a CSB+ tree with contiguous child groups. [Rao and Ross's original research](https://cadmo.ethz.ch/education/lectures/FS17/SDBS/RaoRoss-sigmod00.pdf) provides background on cache-conscious search trees; its reported performance does not apply to this implementation.

With node capacity fixed:

| Operation | Time | Additional storage |
| --- | --- | --- |
| count / isEmpty | O(1) | O(1) |
| find / tryFind / containsKey / min/max | O(log n) comparisons | O(1), except returned options/tuples |
| add / remove / change | O(log n) comparisons and bounded copying per level | O(log n) |
| fold / iter / searches / enumeration | O(n), searches can stop early | Traversal state and callback results |
| map / filter / partition | O(n) | O(n) |
| ofArray / ofList / ofSeq | O(n log n) comparisons worst case; O(n) for ordered input | O(n) |
| toArray / toList | O(n) | O(n) |

Comparison costs depend on key type and contents. Counts use 32-bit integers and additions reject overflow. Bulk construction orders input positions by key, using original position as a tie-breaker, keeps the last equal entry, then packs all levels evenly. It uses temporary sorting and staging arrays; allocation exceeds final tree size. `filter` and `partition` stage their results before packing, rather than preserving arbitrary covered subtrees. `change` currently performs a lookup followed by an edit.

## Semantics and API

`CacheMap<'Key,'Value when 'Key: comparison>` uses F# generic key comparison. Comparison returning zero identifies an existing entry. Adding an equal key replaces both its value and its stored representative, including during construction from duplicate keys. Null string keys and floating-point NaN follow F# key comparison. Keys must retain their ordering while stored, and immutability is shallow: mutable key/value objects remain mutable.

The module includes `empty`, `isEmpty`, `count`, `add`, `remove`, `change`, `containsKey`, `find`, `tryFind`, `map`, `filter`, `partition`, `fold`, `foldBack`, `iter`, `exists`, `forall`, `tryPick`, `pick`, `tryFindKey`, `findKey`, `minKeyValue`, `maxKeyValue`, their `try` variants, `keys`, `values`, and array/list/sequence conversions. Argument order follows F# Map. Callbacks visit ascending keys except `foldBack` and `partition`, which visit descending keys. Missing `find`/`pick`/`findKey` and empty min/max throw `KeyNotFoundException`. `change` invokes its callback once. Values need not support equality or comparison to be stored.

The type exposes `Count`, `IsEmpty`, indexing, `ContainsKey`, `TryFind`, `TryGetValue`, persistent `Add`/`Remove`, sorted `IEnumerable<KeyValuePair<_,_>>`, and `IReadOnlyDictionary<_,_>`. Its structural value equality, hashing, and lexicographic comparison ignore tree shape. Comparing or hashing values that cannot support those operations can throw, as with F# collections. This is a companion type, not a binary substitute for F# Map: it does not implement its mutable dictionary facade, serialization, or every instance member, and hashes need not equal F# Map hashes. The [FSharp.Core implementation](https://github.com/dotnet/fsharp/blob/main/src/FSharp.Core/map.fs) and executable tests serve as behavioral references for the common module operations.

## Benchmarking

The independent BenchmarkDotNet project compares public CacheMap and F# Map operations, with `int` and fixed-format string keys and integer values, at 1,024 and 65,536 entries. Read/edit sources are built through deterministic shuffled inserts for both implementations. Bulk construction is measured separately with ordered and shuffled input. No mutable dictionary edit is treated as equivalent to creating a persistent version.

| Group | Work performed |
| --- | --- |
| Lookup | 1,024 precomputed random hits or in-range misses, normalized per lookup; `tryFind` on both sides |
| Edit | 64 independent edits of the same root, normalized per edit: insert absent key, replace present key, remove present key, remove absent key; all 64 results returned |
| Build | Construct from an existing ordered or shuffled tuple array |
| Fold | Sum integer values as int64 using each module's public fold |
| Transform | Map values by +1, or filter to even values (50% retained) |
| Mixed | 255 evolving edits: update an existing even key, insert an absent odd key, then remove that odd key; retain latest root or all 256 roots, normalized per edit |

Every group can run independently, or use `*` for all. An unfiltered invocation presents BenchmarkDotNet's selection menu. Generic type names appear in the listing, so filters can also select `Int32` or `String`.

```powershell
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --validate-only
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --list flat
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*Lookup*' --job short
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*Edit*' '*Mixed*' --job short
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*' --job short
./benchmarks/CacheMap.Benchmarks/Measure-Retention.ps1
```

`--validate-only` compares the actual benchmark outputs, including every returned mixed-edit snapshot, against F# Map. The separate xUnit suite checks randomized model equivalence, duplicate representative keys, array ownership, short-circuit/callback behavior, sequence disposal, growth/shrinkage and balancing, physical sharing, independent concurrent forks, and saved snapshots.

The retention script runs each key type and implementation three times in fresh, sequential processes. All roots remain alive through full collection. Its baseline keeps the original collections, input payloads, and edit trace alive; the delta measures additional container/history storage, not total collection size or RSS. Both edit paths are warmed before sampling. Every retained root is checked against an independent mutable sorted dictionary after the measurement. Trace keys are allocated before sampling, equally for both implementations. Measurements of allocated bytes and live bytes are distinct.

## Scope of the claim

The implementation provides packed storage and structural sharing. Timings combine comparison cost, instructions, branches, copying, allocation, and GC. They do not isolate cache misses; hardware-counter collection was unavailable in the earlier CacheList study. Reference keys still point to separate string objects, and large struct keys/values make copying expensive. These workloads do not cover custom expensive comparers, wide values, unordered hash-map competition, range-heavy workloads, every insertion distribution, or adversarial keys. A hash trie is a separate design worth studying if sorted order is unnecessary; this ordered tree does not promise average O(1) dictionary lookup.

## Measured findings

**The approach works, with a read/transform bias. F# Map is the better choice for the measured successful-edit workloads and for small-map reads.** Packed leaves do not automatically make every map operation faster.

104 primary parameter cases completed in isolated Release processes (two warmups, five measured iterations targeting 100 ms). Twelve longer string-read confirmations completed with five warmups and twelve measured iterations targeting 200 ms. Both use one launch. Environment: Windows 11, .NET SDK 10.0.401 / runtime 10.0.13, BenchmarkDotNet 0.15.8. CPU identity and power-plan control were unavailable. In this offline environment `NuGetAudit=false` was supplied as a process environment setting to use cached packages without fetching vulnerability metadata.

The table gives mean **CacheMap / F# Map** times at **65,536 entries**. Lookups, individual edits, and mixed edits are normalized per operation. String reads use the longer confirmations; other results use the primary run.

| Operation | Integer keys: CacheMap / Map | String keys: CacheMap / Map |
| --- | ---: | ---: |
| Lookup hit (`tryFind`) | 43.4 / 83.2 ns | 146.4 / 161.6 ns |
| Lookup miss (in range) | 39.6 / 92.8 ns | 138.9 / 173.7 ns |
| Fold | 231.1 / 475.6 µs | 317.3 / 493.5 µs |
| Map values | 302.5 / 1,081.1 µs | 377.2 / 1,053.2 µs |
| Filter (50% retained) | 1.44 / 7.33 ms | 2.16 / 10.00 ms |
| Build ordered input | 3.91 / 18.95 ms | 3.80 / 22.26 ms |
| Build shuffled input | 8.74 / 28.82 ms | 20.05 / 34.54 ms |
| Insert new key | 480.7 / 180.2 ns | 969.6 / 274.5 ns |
| Replace existing key | 528.4 / 165.7 ns | 984.3 / 266.5 ns |
| Remove existing key | 465.5 / 172.6 ns | 939.4 / 275.3 ns |
| Remove absent key | 40.1 / 163.1 ns | 88.7 / 265.2 ns |
| Mixed edits, retain all versions | 460.7 / 164.6 ns | 825.9 / 255.0 ns |

The primary large string read results were noisy; the longer confirmations above resolve that uncertainty for these runs. Some primary construction/filter/transform intervals remain wide, particularly string filtering (CacheMap mean 2.16 ms, reported error 2.94 ms). The direction of its gap is useful evidence, but that short run does not support a precise speedup. Full means, errors, standard deviations, ratios, allocations, and GC statistics are preserved in [the complete reports](cache-map-results.md), including the original noisy readings. These are local workload results, not universal multipliers.

At **1,024 entries**, F# Map wins successful lookups and folds for both key types. Integer hits take 22.7 versus 16.3 ns; string hits take 56.2 versus 45.5 ns. Integer folds take 3.31 versus 1.91 µs, and string folds take 5.16 versus 2.26 µs. A larger-node tree has overhead that small collections may not recover. The integer miss case is close and its error intervals overlap; it should not be advertised as a decisive small-map win.

Allocation also exposes the trade-off. At 65,536 integer keys, `map` allocates about **548 KiB versus 2,691 KiB**, and shuffled construction about **2.55 MiB versus 47.10 MiB**. Those are total allocations during each operation, including intermediates, not final collection sizes. Successful edits reverse the comparison: integer replacement allocates **1,192 versus 781 bytes**, while retained mixed edits allocate **1,182 versus 743 bytes per edit**. Edit results include the shared measurement result array, amortized per operation; even an absent removal therefore reports about 8 bytes although CacheMap returns its original root. Successful `tryFind` reports 24 bytes for the returned option on both implementations; misses and folds report zero.

### Retained histories

Three fresh processes per case retained every root through a full collection, starting with 4,096 entries and performing 1,023 mixed edits. Each retains **1,024 roots**, and all roots are validated against an independent sorted dictionary. The table gives median **additional live managed memory**:

| Key type | CacheMap | F# Map |
| --- | ---: | ---: |
| Int32 | 0.809 MiB | 0.592 MiB |
| String | 1.009 MiB | 0.592 MiB |

Both structures share storage across versions, but **F# Map retains less additional memory in this trace**. Copying packed nodes is more expensive than copying its smaller binary nodes. String-key histories copy arrays of 64-bit references, increasing CacheMap's cost; string objects were preallocated and shared equally. These measurements do not include total base-container footprint. [Raw retention data](cache-map-retention.json) includes allocated/live bytes for all twelve workers; [machine-readable timing data](cache-map-data.json) contains all 116 measured parameter cases and source hashes.

### Correctness and interpretation

The final solution builds with zero warnings/errors, all **93 xUnit cases pass**, and `--validate-only` confirms every actual benchmark result and returned snapshot against F# Map. The tests also catch a subtle compatibility requirement: `partition` predicates run in descending key order, while most other callbacks run ascending. That unbenchmarked method was corrected after measurement; implementations of all timed operations are unchanged. Measured and validated source hashes are recorded with the raw results.

This makes CacheMap a useful candidate for larger ordered maps that are built once or transformed/scanned often. For small maps, sustained successful edits, or long histories of edits, the measured F# Map is preferable. Branch metadata rebuilding, temporary arrays during splits/merges, two traversals in `change`, capacity tuning, and streaming bulk builders remain optimization opportunities; the results characterize this implementation rather than a theoretical limit of persistent B+ trees.

To reproduce the recorded timing settings:

```powershell
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*' --job short --warmupCount 2 --iterationCount 5 --iterationTime 100
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*Lookup<String>*' '*Fold<String>*' --job short --warmupCount 5 --iterationCount 12 --iterationTime 200
```
