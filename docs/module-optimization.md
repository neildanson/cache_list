# Direct tree module operations

Module helpers now traverse leaves and construct tree nodes directly instead of converting through F# Seq or List. Length-preserving transformations retain their input shape and share immutable cumulative counts. Variable-length results use a streaming builder that balances packed leaves before constructing the branches. Slices reuse covered subtrees and copy/rebalance their boundaries. Sorting uses private buffers and stable merges; grouping uses structural hash keys and packed builders.

All 54 xUnit cases pass, including shape and occupancy checks, randomized bulk edits and slicing, persistence, callback order and failure, short-circuiting, stable sorting, null/structural keys, numeric overflow, input length mismatches, and sequence disposal. All 138 benchmark cases also completed an in-process Dry smoke run.

## Local before/after measurements

Measured 19 October 2026 on Windows 11, .NET SDK 10.0.401, runtime 10.0.13, BenchmarkDotNet 0.15.8. The baseline library was built from commit `f7ffab5`; the optimized library was built from the working tree. Both ran the same benchmark driver, inputs, and callbacks. The selected methods are not inline, so each driver calls the implementation in its corresponding library. Both libraries and the benchmark driver were compiled in Release mode.

Selected means at **65,536 integer elements**:

| Operation | Before mean | After mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| Map (+1) | 858.4 us | 67.1 us | 929.53 KB | 372.21 KB |
| Filter (retain even values) | 451.8 us | 343.9 us | 465.52 KB | 215.95 KB |
| CountBy (value modulo 128) | 830.3 us | 650.4 us | 2065.99 KB | 19.68 KB |

Map was approximately 13 times faster in this run and allocated about 60% less. Filter allocated about 54% less, and CountBy about 99% less. These are measurements of the specified workloads, not universal speed guarantees. Small inputs, different callback costs, and different output distributions can change the balance.

The runs used the in-process toolchain with three warmups and five measured iterations of 200 ms. The environment could not expose processor identity or change the power plan; the in-process host reports DEBUG despite Release compilation. Some confidence intervals are wide, especially after optimization: at 65,536 elements the reported mean errors were approximately 28 us for Map, 183 us for Filter, and 157 us for CountBy. Allocation reductions provide clearer evidence than the modest Filter and CountBy timing differences. Use longer isolated runs to draw precise timing conclusions. [Full reports, including both input sizes, errors, and allocations](module-optimization-results.md).

To run the same selected cases on either revision:

```powershell
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --filter '*TreeMap' '*TreeFilter' '*TreeCountBy' --job short --inProcess --warmupCount 3 --iterationCount 5 --iterationTime 200
```

For a normal isolated run of all module comparisons:

```powershell
dotnet run -c Release --project benchmarks/CacheList.Benchmarks -- --anyCategories Module --filter '*' --job short
```
