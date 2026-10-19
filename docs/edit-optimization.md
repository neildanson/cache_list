# Persistent edit optimizations

Three changes reduce allocation in the existing persistent tree algorithms:

- Value replacement shares immutable cumulative count arrays along the changed path. The child and value arrays on that path are still copied.
- Concatenation keeps both input nodes when they meet minimum occupancy and cannot fit in a single node. Underfull roots are still merged or redistributed before becoming children.
- Insertion and joining copy split results directly into their final arrays. Redistribution also copies directly into its final arrays, and removal no longer copies an ancestor's child array before replacing that same region during rebalancing.

## Validation

The Release build passed with zero warnings and errors. All **71 xUnit cases passed**, including the existing randomized edit, snapshot, concurrency, overflow, occupancy, and helper tests. New tests check shared counts on update paths, preserved roots after concatenation, underfull root balancing, reference-type insertions at split boundaries, retained snapshots, and the exact leaf sizes used by the new boundary benchmarks. All **146 benchmark cases** completed a final in-process Dry smoke run; Dry timing is not used below.

## Before and after

Measured 19 October 2026 on Windows 11, .NET SDK 10.0.401, runtime 10.0.13, BenchmarkDotNet 0.15.8. The baseline library was built from commit `267210a` before the tree changes. Both libraries and the benchmark driver were compiled in Release mode. The same benchmark driver and parameters called the baseline and optimized libraries; the selected library methods are not inline.

Both sides ran 22 relevant parameter cases: seven standard operations and four boundary workloads, each at 1,024 and 65,536 elements. A regression test caught that the initial redistribution fixture did not force the intended leaf sizes. The fixture was corrected and the entire eight-case boundary group was rerun against both saved libraries. The original boundary measurements were discarded; the table and full reports contain only the corrected results. The 14 standard cases were unaffected by that fixture correction.

| Operation | N | Before mean | After mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: | ---: |
| Update | 1024 | 151.4 ns | 104.7 ns | 664 B | 512 B |
| Update | 65536 | 354.4 ns | 207.5 ns | 1232 B | 896 B |
| Append | 1024 | 219.1 ns | 173.0 ns | 1.41 KB | 960 B |
| Append | 65536 | 432.0 ns | 338.1 ns | 2.36 KB | 1648 B |
| Prepend | 1024 | 220.0 ns | 190.5 ns | 1.41 KB | 960 B |
| Prepend | 65536 | 429.9 ns | 322.0 ns | 2.36 KB | 1648 B |
| InsertMiddle | 1024 | 214.0 ns | 143.9 ns | 1.41 KB | 960 B |
| InsertMiddle | 65536 | 423.6 ns | 333.9 ns | 2.36 KB | 1648 B |
| RemoveMiddle | 1024 | 137.5 ns | 135.6 ns | 664 B | 664 B |
| RemoveMiddle | 65536 | 339.4 ns | 332.3 ns | 1232 B | 1232 B |
| RemoveLast | 1024 | 156.4 ns | 138.6 ns | 664 B | 664 B |
| RemoveLast | 65536 | 348.6 ns | 340.3 ns | 1232 B | 1232 B |
| Concat | 1024 | 212.56 ns | 33.81 ns | 1592 B | 128 B |
| Concat | 65536 | 63.92 ns | 67.36 ns | 184 B | 184 B |
| RemoveMerge | 1024 | 251.6 ns | 195.3 ns | 1176 B | 904 B |
| RemoveMerge | 65536 | 643.9 ns | 473.8 ns | 2544 B | 2096 B |
| RemoveRedistribute | 1024 | 307.3 ns | 216.5 ns | 1528 B | 1040 B |
| RemoveRedistribute | 65536 | 449.7 ns | 388.8 ns | 2128 B | 1640 B |
| InsertWithoutSplit | 1024 | 135.8 ns | 112.0 ns | 760 B | 728 B |
| InsertWithoutSplit | 65536 | 315.4 ns | 283.0 ns | 1392 B | 1360 B |
| ConcatUnderfullRoot | 1024 | 216.2 ns | 156.7 ns | 1408 B | 960 B |
| ConcatUnderfullRoot | 65536 | 400.6 ns | 327.7 ns | 2384 B | 1648 B |

Update at 65,536 elements was about **1.7 times faster**, allocating **27% less**. Concatenation at 1,024 elements was about **6.3 times faster**, allocating **92% less**. At 65,536 elements, merge removal allocated about 18% less and redistribution removal about 23% less. Insertions that split full nodes also allocate substantially less; insertion into a spare slot saves the single-value temporary array.

These gains depend on the path taken. For the 65,536-element bulk-built input, the two root child arrays fit together, so concatenation still merges them and allocates the same 184 bytes. Standard removal from full leaves requires no rebalancing and allocates the same amount before and after. Timing differences in those unchanged allocation cases are small and should be treated as noise.

The short in-process runs used three warmups and eight measured iterations targeting 200 ms. Several confidence intervals overlap, particularly for small operations. The environment could not report the processor identity or change the power plan, and the in-process host reports DEBUG despite Release compilation. Treat timings as local evidence and use longer isolated runs for precise performance claims. Allocation reductions are the more direct evidence for the avoided temporary arrays. [Full reports with errors, standard deviations, allocations, and GC counts](edit-optimization-results.md).

## Boundary workloads

The standard operation groups use the existing stable bulk-built input. The additional `EditBoundaries` group uses:

- **RemoveMerge:** an append-built tree, removing from a 16-element middle leaf so that it merges with a 16-element sibling into a 31-element leaf. This can propagate rebalancing upwards.
- **RemoveRedistribute:** the reversed shape of an append-built tree, remapped to ascending values, with a full first leaf and a 16-element second leaf. Removing at index 32 forces redistribution into leaves of 23 and 24 elements.
- **InsertWithoutSplit:** a bulk-built tree with index 1 removed beforehand, leaving a 31-element leaf. Inserting at index 1 fills its spare slot.
- **ConcatUnderfullRoot:** a singleton concatenated before a full bulk-built tree, exercising redistribution and any branch splitting on the boundary.

Setup is excluded from measurement. Every operation returns a new result from a stable input. Tests verify the leaf sizes and resulting tree balance instead of relying on assumptions about incremental construction.

To run the selected comparisons (retain the new benchmark fixture when comparing library revisions):

```powershell
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*TreeUpdate' '*TreeAppend' '*TreePrepend' '*TreeInsertMiddle' '*TreeRemoveMiddle' '*TreeRemoveLast' '*TreeConcat' '*EditBoundaries*' --job short --inProcess --warmupCount 3 --iterationCount 8 --iterationTime 200
```

For an isolated run, omit `--inProcess`.
