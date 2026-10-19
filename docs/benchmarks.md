# Local benchmark results

Measured 18 October 2026 on Windows 11, .NET SDK 10.0.401, runtime 10.0.13, BenchmarkDotNet 0.15.8. All **66 cases completed** in about 100 seconds. [Full report, including errors and allocations](benchmark-results.md).

Command:

```powershell
dotnet run -c Release --no-restore --project benchmarks/CacheList.Benchmarks -- --filter '*' --job short --inProcess --warmupCount 1 --iterationCount 3 --iterationTime 100 --exporters markdown
```

This is a short in-process run in a shared development session, with just three measured iterations per case. Several confidence intervals are wide. BenchmarkDotNet could not read the processor identity or change the power plan under the environment's permissions. Its in-process host reports `DEBUG` in the environment header despite the Release build command. These measurements establish that the suite executes and provide initial performance evidence; use longer isolated process runs before making precise speed claims. Some individual iteration lengths fell below BenchmarkDotNet's recommended minimum.

The standard isolated process toolchain was also checked with `--filter '*TreeAppend*' --job Dry`: both parameter cases built and completed successfully. Dry measurements are smoke checks, so their timing is not included here. The Release solution build passed with zero warnings, and all 22 xUnit tests passed.

Selected means at **65,536 integer elements**, rounded for readability:

| Operation | CacheList | Array (copying edits) | F# List |
| --- | ---: | ---: | ---: |
| Append one value | 0.758 us / 2,416 B | 29.0 us / 262,325 B | 828 us / 2,097,212 B |
| Remove middle | 0.674 us / 1,232 B | 27.5 us / 262,217 B | 345 us / 1,048,589 B |
| Replace middle | 0.715 us / 1,232 B | 22.5 us / 262,202 B | 296 us / 1,048,622 B |
| Index middle | 32.4 ns / 0 B | 0.38 ns / 0 B | 32,821 ns / approximately 0 B |
| Concatenate with itself | 0.134 us / 184 B | 73.1 us / 524,386 B | 995 us / 2,097,194 B |

The small concatenation allocation in this case comes from joining two shallow roots while sharing their children. Other shapes require more boundary copying. The indexing path allocates no managed memory. BenchmarkDotNet's tiny non-zero allocation estimates in some reference cases reflect harness overhead at low invocation counts.

Arrays remain the strongest baseline for direct indexing and bulk copying. At 1,024 elements, several copying array edits beat the tree's runtime while allocating more memory. Lists retain their constant-time, low-allocation prepend advantage. The tree's value is efficient persistent indexed edits and concatenation with contiguous leaf storage; no claim of universal superiority follows from this run. Traversal results depend on callback inlining, enumeration dispatch, and runtime optimization, so they should not be interpreted as an isolated measurement of cache locality.

The suite includes both direct collection folds (`*Iterate`) and the common F# sequence interface (`*Sequence`). It measures one operation on a stable, bulk-built input, rather than a distribution of random tree shapes or long retained-version workloads. Tests separately exercise repeated growth, shrinkage, and persistence. Future performance work can add mixed edit workloads and retained-snapshot memory measurements.
