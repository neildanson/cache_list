# Evidence for persistence, immutability, and locality

The experiments support a narrower, defensible claim: **CacheList preserves immutable versions while providing packed value storage, cheap indexed edits, and substantially faster integer traversal than F# List.** They also expose real limits: scattered reference payloads largely erase that traversal advantage, and .NET ImmutableList wins the measured small integer mixed-edit workload and retains less incremental memory.

**CPU cache misses were not measured.** Windows denied the profiler session. Timing, allocation, and object layout can support a cache-conscious design, but do not prove a reduction in L1/L2/LLC misses or identify how much of a speedup comes from caches.

## 1. Allocation-free traversal

All input trees undergo 8,190 deterministic mixed edits before measurement. For the Bulk case the resulting values are rebuilt into a packed tree; for Edited the naturally edited shape is retained. Both cases contain exactly the same logical values and requested element count. Arrays and lists contain the same values in the same order. No data construction happens inside the timed scans.

The raw controls use direct loops over arrays and linked lists and a white-box recursive tree traversal with direct loops over leaf arrays. They compute the same int64 key sum, without per-element callbacks or sequence adapters. The reference payload carries eight int64 fields, is allocated in ordinal order, and is shuffled before collection construction; every collection uses the same objects. The 64-byte struct is verified with Marshal.SizeOf, and the scan reads its first int64 field. This measures key projection, not processing all eight fields.

| Edited input, raw traversal | Elements | CacheList (ms) | Array (ms) | F# List (ms) |
| --- | ---: | ---: | ---: | ---: |
| Int32 | 4194304 | 2.109 | 0.909 | 8.720 |
| Scattered object references | 1048576 | 9.664 | 6.678 | 9.771 |
| 64-byte value structs | 1048576 | 4.796 | 2.286 | 5.878 |

The edited integer case is about **4.1 times faster than F# List**. Its three implementations all report **zero managed bytes per scan**. Thus allocation or sequence-adapter allocation inside the scan cannot explain that result. Arrays remain faster. Instruction count, traversal mechanics, branch prediction, and cache behaviour are still combined in the timing.

The public module APIs also show the benefit: folding 4,194,304 edited integers takes **2.259 ms for CacheList**, **0.937 ms for Array**, and **9.482 ms for F# List**, all reporting zero allocation. The raw tree traversal is a control, not a replacement for this public-API result.

**The reference result is a counterexample to a universal locality claim.** CacheList and F# List are effectively tied in that large scattered-object scan; overlapping error intervals do not support a meaningful win. Packing references cannot pack their targets. The reference-list case reports a small nonzero allocation in the full report, so the zero-allocation statement above is specifically about the integer controls. Large structs also narrow the advantage, especially on edited trees: their tree scan rises from 2.702 ms bulk-built to 4.796 ms edited, compared with 5.878 ms for F# List.

## 2. Mixed persistent edits, not just bulk-built inputs

Each batch starts with 4,096 elements in an edit-shaped tree and performs 256 deterministic, uniformly indexed update/insert/remove operations. Complete triples preserve length; intermediate lengths vary by one. Every replacement is prepared outside the timed region. Array edits copy; F# List and CacheList preserve prior versions through their normal persistent APIs. A second parameter either keeps only the latest root or retains every version within the batch. MemoryDiagnoser reports values normalized per edit, not per batch.

| Retain all 257 roots, 4,096 starting elements | CacheList mean / allocation per edit | Array | F# List |
| --- | ---: | ---: | ---: |
| Int32 | 215.1 ns / 697 B | 772.3 ns / 16419 B | 43,662.7 ns / 67215 B |
| References | 278.9 ns / 785 B | 8,682.9 ns / 32803 B | 46,566.5 ns / 67215 B |
| 64-byte values | 327.6 ns / 2 KB | 53,926.8 ns / 256.05 KB | 144,434.1 ns / 180.5 KB |

These results include algorithmic complexity, copying, and GC effects. They do not isolate cache locality. Histories from one benchmark invocation are released before subsequent invocations; the separate experiment below measures histories held through a full collection.

## 3. Actual live memory with old versions held

Additional live managed memory is measured after a full collection while **every snapshot root remains reachable**. The initial collections, input payloads, edit trace, and independent model remain alive across both samples. Replacement payloads are preallocated equally for all collections, so this measures additional container/version storage, not creation of application objects. Each case runs three times in a fresh process; the table uses medians.

| Payload | Starting elements | Edits / retained roots | CacheList (MiB) | Array (MiB) | F# List (MiB) | ImmutableList (MiB) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Int | 4096 | 2048 / 2049 | 1.283 | 32.068 | 128.886 | 1.162 |
| Reference | 4096 | 256 / 257 | 0.181 | 8.003 | 16.409 | 0.144 |
| Wide64 | 4096 | 256 / 257 | 0.484 | 64.013 | 45.085 | 0.303 |

For integers, preserving 2,049 versions uses approximately **1.28 MiB additional live memory for CacheList**, versus **32.07 MiB for arrays** and **128.89 MiB for F# List**. ImmutableList uses less, about **1.16 MiB**. The result demonstrates sharing against copying/linked baselines, and does not establish that CacheList minimizes retained memory among persistent indexed collections.

The sample includes small measurement bookkeeping and is rounded to MiB. It is not peak RSS or the total base-collection footprint. The raw data also records baseline/total live bytes, committed heap bytes, fragmentation, allocations, and GC pauses. Those are distinct metrics. Forced collection occurs outside the measured edit interval.

## 4. Ten thousand edits with every version retained

For each payload type, the tree starts with 65,536 elements after 8,190 mixed edits, then performs another 10,000 edits while retaining **10,001 roots**. Every retained root is checked against independently modelled length and key sum; the final tree's occupancy, cumulative counts, and equal leaf depth are checked as well.

| Payload, 65,536 starting elements | Additional live MiB | Allocated MiB | Observed work GC collections (Gen0 / Gen1 / Gen2) | Median work GC pause (ms) |
| --- | ---: | ---: | --- | ---: |
| Int | 9.436 | 10.019 | 0 / 0 / 0 | 0 |
| Reference | 10.311 | 10.938 | 0 / 0 / 0 | 0 |
| Wide64 | 23.223 | 24.087 | 1 / 0 / 0 | 4.01 |

The collection/GC counts above are observations from this short work interval, not a promise that the collection avoids GC. Across all 45 isolated retention workers, **120,765 retained roots** passed the length/checksum checks. Additional exact whole-sequence comparisons test all fields and order against a mutable independent model for 1,024 edits per payload, checking saved old versions after later changes. These tests cover CacheList, arrays, F# List, and ImmutableList. The benchmark entrypoint additionally checks the actual scan/fold outputs and compares every returned snapshot in the measured 256-edit batch against the copying-array result for both retention modes, including full struct fields and reference identity. The production library's **71 xUnit cases also pass**.

This is executable evidence, not a formal proof for all possible programs. Immutability remains shallow: mutable objects supplied by an application can change even when their containing collection is persistent.

## 5. An indexed persistent competitor

System.Collections.Immutable.ImmutableList is also measured, so indexed edits are not compared only with linear-copy baselines. On the same retained 256-edit integer trace:

| Implementation | Mean per edit | Allocation per edit |
| --- | ---: | ---: |
| CacheList | 217.8 ns | 697 B |
| ImmutableList | 135.4 ns | 591 B |

**ImmutableList wins this edit case and uses less incremental live memory in all three retention payload cases.** CacheList is not the universally best persistent indexed collection.

CacheList wins the measured public read comparison: folding an edited 1,048,576-element integer collection takes 0.588 ms, versus 16.791 ms for ImmutableList's public struct enumerator loop. Both report zero allocation. ImmutableList.ForEach was also checked, using a cached Action prepared outside timing; the edited million-element mean was 24.329 ms with a wide reported error of 11.056 ms, also reporting zero allocation. Its precise ranking against the enumerator is uncertain. Both public APIs are preserved in the full results. These comparisons include public traversal implementation differences; they are not measurements of cache misses or pure comparisons of tree layouts.

## 6. Representation evidence

The study directly visits the actual tree representation:

| 4,194,304 integers | Leaf arrays | Branch nodes | Tree storage objects | F# List cons nodes | Array storage objects |
| --- | ---: | ---: | ---: | ---: | ---: |
| Bulk | 131072 | 4229 | 274832 | 4194304 | 1 |
| Edited | 133748 | 6352 | 286553 | 4194304 | 1 |

The count includes leaf nodes and their arrays, branch nodes and their child/count arrays, and the CacheList wrapper. It excludes common application payload objects. The generated single-version inputs contain no duplicate subtree aliases. This establishes far fewer storage objects than one linked node per element, and contiguous storage within each leaf.

Published leaf arrays have exactly their occupied length; unused logical capacity is not a block of reserved empty element slots. Mean leaf occupancy and height are also saved in the raw data. Fewer objects and packed values are structural evidence; hardware cache-line traffic and miss counts still require measurement.

## 7. Hardware-counter limit

Windows enumerates CacheMisses and DcacheMisses performance-counter sources. Starting a real, uniquely named kernel profiling session through TraceEvent failed with:

```
System.UnauthorizedAccessException: Error Starting ETW: Access Denied
(Administrator rights required to start ETW)
```

The raw probe records KernelProfilingEnabled=false. No counter values, IPC numbers, or miss rates are inferred from timings. Windows hardware counter APIs depend on configured PMU resources; [Microsoft documents the kernel-side counter configuration](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/nf-ntddk-kesethardwarecounterconfiguration). The specific blocked path here is recorded by the actual ETW exception, not assumed from platform documentation.

**A reduction in CPU cache misses remains unproved in this environment.** Completing that claim requires a permitted counter collection with misses/instructions/cycles normalized per element on the same workloads, including repetitions and the scattered-reference counterexample. Permission to start profiling alone would not constitute a counter measurement.

## Reproduction and environment

The standalone project compiles the unmodified production source directly into the benchmark assembly to inspect internal nodes; it changes no collection algorithm. Private-layout traversal is explicitly labelled separately from public API benchmarks. The source SHA-256 is `A9759CE623A6F4824CE81F455525B4B7870B5EF3C842A8E801E68C25D4850A3E`.

Measurements: Windows 11, .NET SDK 10.0.401, runtime 10.0.13, BenchmarkDotNet 0.15.8. **78 primary, 12 persistent-peer, and 4 additional public traversal parameter cases completed**, using isolated Release processes, three warmups, and eight measured iterations targeting 150 ms. CPU identity/power-plan control were unavailable. Short runs have uncertainty; all errors and GC statistics are retained. In this offline session NuGetAudit=false was supplied as a process environment property so restore could use cached packages without fetching vulnerability metadata.

```powershell
dotnet run -c Release --project benchmarks/CacheList.ProofBenchmarks -- --validate-only
dotnet run -c Release --project benchmarks/CacheList.ProofBenchmarks -- --filter '*' --job short --warmupCount 3 --iterationCount 8 --iterationTime 150
./benchmarks/CacheList.ProofBenchmarks/Measure-Retention.ps1 -Repeats 3
dotnet run -c Release --project benchmarks/CacheList.ProofBenchmarks -- --layout
dotnet run -c Release --project benchmarks/CacheList.ProofBenchmarks -- --probe
```

[Full benchmark reports](persistence-locality-results.md) and [machine-readable retention, layout, probe, and benchmark data](persistence-locality-data.json).
