# CacheMap benchmark suite

BenchmarkDotNet compares CacheMap with F# Map using integer and string keys, integer values, and 1,024 / 65,536 entries. All groups report time and managed allocations. Input construction happens outside measurement, except in the construction benchmarks themselves. Sources are built with deterministic shuffled inserts; edit benchmarks preserve the source and return their new versions.

Run from the repository root:

```powershell
# Choose one or more groups interactively:
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --job short
# One group:
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*Partition*' --job short
# Several groups:
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*Lookup*' '*TryGetValue*' '*ContainsKey*' --job short
# Integer keys only:
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*<Int32>*' --job short
# All groups:
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*' --job short
# List cases or check execution quickly:
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --list flat
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --filter '*' --job Dry
# Check benchmark results against F# Map, without timing:
dotnet run -c Release --project benchmarks/CacheMap.Benchmarks -- --validate-only
```

| Group | Cases |
| --- | --- |
| `Lookup` | `tryFind`: 1,024 random hits or in-range misses per invocation |
| `ContainsKey` | The same hit/miss probes, returning the number found |
| `TryGetValue` | The same probes through the out-parameter API, summing successful values without option results |
| `Find` | 1,024 successful lookups, summing returned values |
| `Edit` | 64 independent inserts, replacements, successful removals, or absent removals from one stable root |
| `Change` | 64 independent callback-based insert, update, remove, or absent/no-op operations |
| `Build` | Construction from ordered or shuffled tuple arrays |
| `Fold` / `FoldBack` | Forward or reverse public folds, summing values as int64 |
| `Iteration` | Public `iter` with an accumulating callback |
| `Transform` | Value mapping (+1) and filtering (even values, 50% retained) |
| `Partition` | Split into two results with nominal selectivities 0%, 50%, and 100% |
| `Enumeration` | Direct key/value entries, tuple sequences, keys, or values, fully consumed |
| `ToArray` / `ToList` | Materialize every entry in the requested representation |
| `Search` | `tryPick` finds the first, middle, or last value, or scans the entire map without a match |
| `Extremes` | Minimum or maximum key/value pair |
| `Mixed` | 255 evolving update/insert/remove operations, keeping the latest root or all 256 roots |

There are **18 independently selectable groups and 312 parameter cases** across both implementations, key types, sizes, and workload parameters. Batched lookup/edit timings and allocations are normalized per operation. Other cases measure a complete scan, transform, construction, or conversion. Enumeration sums values; the keys-only case sums structural key hashes, so its timing includes hashing. Partition selectivity uses `value % 100 < Percent`, which approximates 50% when N is not divisible by 100. Full result containers are returned for transforms and edits, including both partition outputs.

`--validate-only` exercises every parameter combination and compares returned collections, scalar results, search outcomes, and saved versions with F# Map. The new change cases also verify that the source root retains its original contents. This validation is separate from timing. `--job Dry` checks the BenchmarkDotNet toolchain and execution paths; its timings are not performance conclusions.

BenchmarkDotNet writes reports under `BenchmarkDotNet.Artifacts/results/` by default; pass `--artifacts <directory>` to keep a run separate. For live-memory measurements with every historical version retained, use [Measure-Retention.ps1](Measure-Retention.ps1). [The design and recorded comparison](../../docs/cache-map.md) describe the persistent representation, earlier measured results, and their limitations. Those earlier results cover the original six groups; the expanded suite does not replace them with smoke-test timings.
