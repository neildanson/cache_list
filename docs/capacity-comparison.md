# Why capacity 32 and minimum occupancy 16?

The measurements support **32 as a balanced default**, with **16 as the strongest minimum occupancy compatible with the current two-way split algorithm**. They do not establish 32 as a universal performance optimum. Capacity 16 reduces many edit allocations; capacity 64 improves bulk construction, mapping, and traversal. The appropriate choice depends on the workload, element type, and tree shape.

## Why the minimum is half capacity

A full 32-entry node becomes 33 entries on insertion. Splitting it into two valid nodes requires `2 * minimum <= 33`, so the largest integer minimum is 16. The existing split is exactly 16 + 17. A minimum of 17 could not make both results valid.

On removal, a minimally occupied node can shrink to 15 entries. Combining it with a minimally occupied sibling gives 15 + 16 = 31, which fits in one 32-entry node. Larger siblings can be redistributed instead. Root nodes are exempt from the minimum occupancy rule.

A smaller minimum is possible, but permits emptier trees. Half capacity provides the strongest density guarantee available with these local two-way splits and merges. A higher minimum would require a different balancing algorithm. This structural justification is separate from choosing the maximum capacity.

## What was measured

A new standalone benchmark project compiles three variants from the current library source, changing only the namespace, maximum capacity, and minimum occupancy: **16/8, 32/16, and 64/32**. A round-trip source comparison confirmed those are the only text changes, apart from line endings. This avoids a runtime capacity switch or delegate dispatch inside measured operations.

The measured library source is commit `29d2f5f`, SHA-256 `A9759CE623A6F4824CE81F455525B4B7870B5EF3C842A8E801E68C25D4850A3E`. The production capacity remains 32/16.

All **120 comparisons completed**: ten operations, three capacities, and four input sizes (1,024; 65,521; 65,536; 1,000,000). Each starts from the same ascending integer values in stable, bulk-built trees. The 65,521-element input has a spare slot in its last leaf at every capacity; the 65,536-element input fills every leaf and forces splits on append. This exposes both paths rather than drawing conclusions only from a favourable split boundary.

Random indexing uses 256 deterministic, uniformly selected indices per invocation (seed 1771). BenchmarkDotNet normalizes its result per index. The same batch repeats, so these are warm random lookups, not measurements of cold-cache or streaming random access. Updates, insertion, and removal use the middle index. Fold accumulates into int64, map adds one, construction starts from an existing array, and concatenation joins the input with itself. Every result is returned to the harness.

## Results

Selected means; lower is better. RemoveMiddle uses the longer confirmation run described below. All other rows use the primary run. Both the primary and confirmation reports are preserved.

| Operation, 1,000,000 integers | Capacity 16 | Capacity 32 | Capacity 64 |
| --- | ---: | ---: | ---: |
| IndexRandom | 33.1 ns | 28.4 ns | 24.1 ns |
| Update | 260.0 ns | 235.1 ns | 243.3 ns |
| Append | 251.8 ns | 292.8 ns | 420.3 ns |
| Prepend | 351.8 ns | 350.4 ns | 427.3 ns |
| InsertMiddle | 365.6 ns | 375.9 ns | 529.9 ns |
| RemoveMiddle (confirmation) | 355.8 ns | 402.8 ns | 478.9 ns |
| Concat | 33.6 ns | 29.0 ns | 49.2 ns |
| Fold | 531 us | 372 us | 305 us |
| Map | 2.14 ms | 1.48 ms | 898 us |
| Build | 7.72 ms | 5.16 ms | 2.98 ms |

| Allocation per operation, 1,000,000 integers | Capacity 16 | Capacity 32 | Capacity 64 |
| --- | ---: | ---: | ---: |
| Update | 864 B | 1128 B | 1528 B |
| Append | 1432 B | 1768 B | 2312 B |
| InsertMiddle | 1656 B | 1992 B | 2424 B |
| Map | 7584.8 KB | 5678.31 KB | 4776.38 KB |
| Build | 8985.17 KB | 6332.46 KB | 5092.85 KB |

At one million elements, capacity 32 reduced fold time by about 30% and construction time by about 33% relative to capacity 16. Relative to capacity 64, capacity 32 appended about 30% faster and allocated about 24% less per append. Those are concrete reasons to retain the middle setting for a collection balancing traversal and persistent edits.

Capacity 64 is better for the measured build/map/fold workloads and large random lookups. Capacity 16 uses less memory per measured edit and appends faster at one million elements. Capacity 32's update means are close to the fastest, but their overlapping confidence intervals do not establish a decisive win. Allocation per edit is not the same as total retained memory: smaller nodes also require more nodes in the base collection.

## Size and shape matter

At 1,024 elements, random indexing measured 10.123 ns / 6.542 ns / 6.962 ns for capacities 16 / 32 / 64 respectively: capacity 32 was fastest there. At one million elements, capacity 64 was fastest. Concatenation is particularly sensitive to root occupancy: capacity 32 measured 24.75 ns at 1,024 elements versus 52.64 ns at 16 and 81.25 ns at 64, but capacity 16 wins at some other input sizes. All sizes and confidence intervals appear in the full reports.

Append means and allocation on either side of a full-leaf boundary:

| Append input size | Capacity 16 | Capacity 32 | Capacity 64 |
| --- | ---: | ---: | ---: |
| 65521 | 216.2 ns / 1080 B | 261.7 ns / 1360 B | 241.6 ns / 1544 B |
| 65536 | 284.4 ns / 1520 B | 306.7 ns / 1648 B | 326.5 ns / 1728 B |

The number of branch levels in these bulk-built inputs explains part of the trade-off:

| Elements | Capacity 16 | Capacity 32 | Capacity 64 |
| --- | ---: | ---: | ---: |
| 1024 | 2 | 1 | 1 |
| 65521 | 3 | 3 | 2 |
| 65536 | 3 | 3 | 2 |
| 1000000 | 4 | 3 | 3 |

Wider nodes reduce node count and can reduce depth. That helps reads, bulk traversal, and storage overhead, but copying larger arrays increases the cost of persistent edits. At capacity 32 an int leaf's maximum payload is 128 bytes; on this 64-bit process a full branch's child-reference and cumulative-count payloads are 256 and 128 bytes in separate arrays, before headers and wrappers. The choice is a depth/copying/density trade-off, not a claim that a node fits a cache line.

## Removal confirmation

The primary capacity-32 removal mean at one million elements was 587.7 ns with a reported error of 231.95 ns, too noisy for a useful comparison. All three capacities were rerun on the same input with five warmups and sixteen measured iterations targeting 200 ms, still in isolated Release processes:

| Capacity | Mean | Reported error | Allocation |
| --- | ---: | ---: | ---: |
| 16 | 355.8 ns | 7.60 ns | 1.19 KB |
| 32 | 402.8 ns | 6.57 ns | 1.55 KB |
| 64 | 478.9 ns | 18.10 ns | 2.07 KB |

The confirmation supports the smaller-node edit trade-off: capacity 16 is faster and allocates less, capacity 32 is intermediate, and capacity 64 is more expensive. The original results remain in the full report; the summary table uses these more precise confirmation measurements. This adds three confirmation cases to the 120-case primary comparison.

## Validation and limitations

Each generated variant passed model comparisons, occupancy and equal-depth checks, cumulative counts, growth/removal, transforms, concatenation, 1,500 randomized edits with slicing, and retained snapshots. Validation includes the one-million-element input. The production library's 71 xUnit cases also passed, and the standalone study rebuilt without warnings or errors.

Measured on 19 October 2026, Windows 11, .NET SDK 10.0.401, runtime 10.0.13, BenchmarkDotNet 0.15.8. The workload used **isolated Release processes**, one launch, three warmups, and eight measured iterations targeting 150 ms. Processor identity and power-plan changes were unavailable in this environment. The launcher reports DEBUG; the isolated workload processes report the Release runtime without that marker. Short runs, shared machine activity, and warm inputs limit precise timing claims; full error intervals are preserved.

These experiments use integer elements and bulk-built starting trees. They do not establish an optimum for larger value types, reference-heavy data, long retained-version workloads, or trees shaped by sustained mixed edits. Nor do they independently tune occupancy ratios: every candidate uses the same half-full rule. No weighted average is presented as a universal workload score.

[Full reports for all sizes, allocations, GC counts, ratios, and error intervals](capacity-comparison-results.md).

## Reproducing the comparison

The standalone study generates its sources under obj using PowerShell 7 (pwsh); it does not require changes to the shipping library or the normal benchmark suite.

```powershell
dotnet run -c Release --project benchmarks/CacheList.CapacityBenchmarks -- --validate-only
dotnet run -c Release --project benchmarks/CacheList.CapacityBenchmarks -- --filter '*' --job short --warmupCount 3 --iterationCount 8 --iterationTime 150
```

In this offline session, `$env:NuGetAudit='false'` was supplied to allow restores from cached packages without fetching unavailable vulnerability metadata. This was a process environment setting for the experiment.

The focused confirmation command was:

```powershell
dotnet run -c Release --project benchmarks/CacheList.CapacityBenchmarks -- --filter '*RemoveMiddle*1000000*' --job short --warmupCount 5 --iterationCount 16 --iterationTime 200
```
