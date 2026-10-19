# Persistence and locality evidence: full results

[Interpretation, methodology, and limits](persistence-locality-proof.md). These are timings and allocation/GC measurements, not CPU cache-miss counts.

## TraverseInt

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method    | N       | Edited | Mean        | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------- |-------- |------- |------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **ScanTree**  | **65536**   | **False**  |    **20.89 μs** |   **0.442 μs** |   **0.196 μs** |  **1.48** |    **0.02** |         **-** |          **NA** |
| ScanArray | 65536   | False  |    14.13 μs |   0.206 μs |   0.108 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 65536   | False  |    61.22 μs |   1.435 μs |   0.751 μs |  4.33 |    0.06 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **65536**   | **True**   |    **47.76 μs** |   **1.799 μs** |   **0.941 μs** |  **3.41** |    **0.07** |         **-** |          **NA** |
| ScanArray | 65536   | True   |    14.00 μs |   0.186 μs |   0.097 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 65536   | True   |    61.10 μs |   0.402 μs |   0.178 μs |  4.36 |    0.03 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **1048576** | **False**  |   **358.44 μs** |   **4.628 μs** |   **2.421 μs** |  **1.59** |    **0.01** |         **-** |          **NA** |
| ScanArray | 1048576 | False  |   224.78 μs |   2.807 μs |   1.468 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 1048576 | False  | 1,904.77 μs |  62.310 μs |  32.590 μs |  8.47 |    0.15 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **1048576** | **True**   |   **502.15 μs** |   **7.286 μs** |   **3.811 μs** |  **2.24** |    **0.02** |         **-** |          **NA** |
| ScanArray | 1048576 | True   |   224.68 μs |   3.770 μs |   1.972 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 1048576 | True   | 1,905.61 μs |  56.088 μs |  29.335 μs |  8.48 |    0.14 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **4194304** | **False**  | **1,463.67 μs** |  **39.939 μs** |  **20.889 μs** |  **1.61** |    **0.02** |         **-** |          **NA** |
| ScanArray | 4194304 | False  |   906.61 μs |  12.477 μs |   6.526 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 4194304 | False  | 8,593.28 μs | 270.823 μs | 141.646 μs |  9.48 |    0.16 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **4194304** | **True**   | **2,108.77 μs** |  **74.800 μs** |  **39.122 μs** |  **2.32** |    **0.04** |         **-** |          **NA** |
| ScanArray | 4194304 | True   |   909.42 μs |   7.551 μs |   3.352 μs |  1.00 |    0.00 |         - |          NA |
| ScanList  | 4194304 | True   | 8,719.59 μs | 225.664 μs | 118.027 μs |  9.59 |    0.13 |         - |          NA |


## TraverseReference

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method    | N       | Edited | Mean        | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------- |-------- |------- |------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
| **ScanTree**  | **65536**   | **False**  |    **56.10 μs** |     **0.614 μs** |   **0.272 μs** |  **1.37** |    **0.01** |         **-** |          **NA** |
| ScanArray | 65536   | False  |    40.95 μs |     0.568 μs |   0.297 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 65536   | False  |    70.93 μs |     0.941 μs |   0.492 μs |  1.73 |    0.02 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **ScanTree**  | **65536**   | **True**   |    **85.83 μs** |     **0.748 μs** |   **0.332 μs** |  **2.14** |    **0.01** |         **-** |          **NA** |
| ScanArray | 65536   | True   |    40.18 μs |     0.480 μs |   0.251 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 65536   | True   |    71.48 μs |     1.090 μs |   0.484 μs |  1.78 |    0.02 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **ScanTree**  | **1048576** | **False**  | **6,882.22 μs** |   **286.962 μs** | **150.087 μs** |  **1.05** |    **0.03** |         **-** |          **NA** |
| ScanArray | 1048576 | False  | 6,577.61 μs |   334.102 μs | 174.742 μs |  1.00 |    0.04 |         - |          NA |
| ScanList  | 1048576 | False  | 9,586.65 μs |   570.206 μs | 253.175 μs |  1.46 |    0.05 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **ScanTree**  | **1048576** | **True**   | **9,664.49 μs** | **1,596.980 μs** | **835.251 μs** |  **1.45** |    **0.13** |         **-** |          **NA** |
| ScanArray | 1048576 | True   | 6,677.57 μs |   512.571 μs | 227.585 μs |  1.00 |    0.04 |         - |          NA |
| ScanList  | 1048576 | True   | 9,770.51 μs |   486.190 μs | 254.287 μs |  1.46 |    0.06 |     771 B |          NA |


## TraverseWide64

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method    | N       | Edited | Mean        | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------- |-------- |------- |------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **ScanTree**  | **65536**   | **False**  |    **39.10 μs** |   **0.784 μs** |   **0.348 μs** |  **1.14** |    **0.02** |         **-** |          **NA** |
| ScanArray | 65536   | False  |    34.27 μs |   1.115 μs |   0.583 μs |  1.00 |    0.02 |         - |          NA |
| ScanList  | 65536   | False  |    84.87 μs |   1.431 μs |   0.748 μs |  2.48 |    0.04 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **65536**   | **True**   |    **78.65 μs** |   **0.893 μs** |   **0.467 μs** |  **2.34** |    **0.02** |         **-** |          **NA** |
| ScanArray | 65536   | True   |    33.62 μs |   0.475 μs |   0.248 μs |  1.00 |    0.01 |         - |          NA |
| ScanList  | 65536   | True   |    83.81 μs |   0.636 μs |   0.227 μs |  2.49 |    0.02 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **1048576** | **False**  | **2,702.23 μs** |  **81.124 μs** |  **36.020 μs** |  **1.18** |    **0.03** |         **-** |          **NA** |
| ScanArray | 1048576 | False  | 2,285.12 μs |  89.460 μs |  46.789 μs |  1.00 |    0.03 |         - |          NA |
| ScanList  | 1048576 | False  | 5,737.28 μs | 208.379 μs |  92.521 μs |  2.51 |    0.06 |         - |          NA |
|           |         |        |             |            |            |       |         |           |             |
| **ScanTree**  | **1048576** | **True**   | **4,795.61 μs** | **192.606 μs** |  **85.518 μs** |  **2.10** |    **0.07** |         **-** |          **NA** |
| ScanArray | 1048576 | True   | 2,286.25 μs | 125.391 μs |  65.582 μs |  1.00 |    0.04 |         - |          NA |
| ScanList  | 1048576 | True   | 5,877.88 μs | 339.845 μs | 177.746 μs |  2.57 |    0.10 |         - |          NA |


## PublicFoldInt

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method    | N       | Edited | Mean        | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------- |-------- |------- |------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
| **FoldTree**  | **65536**   | **False**  |    **24.23 μs** |     **0.282 μs** |   **0.148 μs** |  **1.73** |    **0.02** |         **-** |          **NA** |
| FoldArray | 65536   | False  |    14.03 μs |     0.287 μs |   0.150 μs |  1.00 |    0.01 |         - |          NA |
| FoldList  | 65536   | False  |    55.89 μs |     0.741 μs |   0.387 μs |  3.98 |    0.05 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **FoldTree**  | **65536**   | **True**   |    **53.76 μs** |     **2.520 μs** |   **1.318 μs** |  **3.81** |    **0.09** |         **-** |          **NA** |
| FoldArray | 65536   | True   |    14.10 μs |     0.146 μs |   0.065 μs |  1.00 |    0.01 |         - |          NA |
| FoldList  | 65536   | True   |    56.55 μs |     0.417 μs |   0.218 μs |  4.01 |    0.02 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **FoldTree**  | **1048576** | **False**  |   **406.88 μs** |     **9.599 μs** |   **4.262 μs** |  **1.75** |    **0.03** |         **-** |          **NA** |
| FoldArray | 1048576 | False  |   232.92 μs |     5.407 μs |   2.828 μs |  1.00 |    0.02 |         - |          NA |
| FoldList  | 1048576 | False  | 2,042.75 μs |   127.059 μs |  56.415 μs |  8.77 |    0.25 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **FoldTree**  | **1048576** | **True**   |   **789.70 μs** |    **80.745 μs** |  **42.231 μs** |  **3.45** |    **0.18** |         **-** |          **NA** |
| FoldArray | 1048576 | True   |   229.10 μs |     2.891 μs |   1.512 μs |  1.00 |    0.01 |         - |          NA |
| FoldList  | 1048576 | True   | 2,003.09 μs |    75.454 μs |  39.464 μs |  8.74 |    0.17 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **FoldTree**  | **4194304** | **False**  | **1,742.47 μs** |    **30.065 μs** |  **15.725 μs** |  **1.84** |    **0.04** |         **-** |          **NA** |
| FoldArray | 4194304 | False  |   946.15 μs |    38.807 μs |  20.297 μs |  1.00 |    0.03 |         - |          NA |
| FoldList  | 4194304 | False  | 9,011.13 μs |   336.874 μs | 120.133 μs |  9.53 |    0.23 |         - |          NA |
|           |         |        |             |              |            |       |         |           |             |
| **FoldTree**  | **4194304** | **True**   | **2,259.21 μs** |    **83.948 μs** |  **43.906 μs** |  **2.41** |    **0.07** |         **-** |          **NA** |
| FoldArray | 4194304 | True   |   936.71 μs |    40.848 μs |  21.364 μs |  1.00 |    0.03 |         - |          NA |
| FoldList  | 4194304 | True   | 9,481.67 μs | 1,097.987 μs | 574.268 μs | 10.13 |    0.62 |         - |          NA |


## MixedInt

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method    | Retain | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------- |------- |------------:|------------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| **EditTree**  | **False**  |    **217.9 ns** |     **6.09 ns** |     **3.19 ns** |  **0.35** |    **0.01** | **0.0412** | **0.0028** |     **689 B** |        **0.04** |
| EditArray | False  |    624.3 ns |    48.93 ns |    25.59 ns |  1.00 |    0.05 | 0.9802 | 0.1077 |   16411 B |        1.00 |
| EditList  | False  |  8,068.5 ns |   500.52 ns |   261.78 ns | 12.94 |    0.64 | 4.0163 | 1.9806 |   67207 B |        4.10 |
|           |        |             |             |             |       |         |        |        |           |             |
| **EditTree**  | **True**   |    **215.1 ns** |     **5.82 ns** |     **2.58 ns** |  **0.28** |    **0.01** | **0.0409** | **0.0146** |     **697 B** |        **0.04** |
| EditArray | True   |    772.3 ns |    59.81 ns |    31.28 ns |  1.00 |    0.05 | 0.9766 | 0.9410 |   16419 B |        1.00 |
| EditList  | True   | 43,662.7 ns | 2,541.05 ns | 1,329.02 ns | 56.61 |    2.68 | 3.9063 | 3.5156 |   67215 B |        4.09 |


## MixedReference

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method    | Retain | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------- |------- |------------:|------------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| **EditTree**  | **False**  |    **276.7 ns** |     **3.46 ns** |     **1.81 ns** |  **0.21** |    **0.01** | **0.0452** | **0.0054** |     **777 B** |        **0.02** |
| EditArray | False  |  1,310.2 ns |   132.04 ns |    58.63 ns |  1.00 |    0.06 | 1.9531 | 0.3859 |   32795 B |        1.00 |
| EditList  | False  | 10,325.9 ns | 1,147.12 ns |   599.96 ns |  7.90 |    0.55 | 3.9714 | 1.9531 |   67207 B |        2.05 |
|           |        |             |             |             |       |         |        |        |           |             |
| **EditTree**  | **True**   |    **278.9 ns** |     **5.15 ns** |     **2.29 ns** |  **0.03** |    **0.00** | **0.0463** | **0.0178** |     **785 B** |        **0.02** |
| EditArray | True   |  8,682.9 ns |   202.68 ns |   106.00 ns |  1.00 |    0.02 | 1.9531 | 1.9043 |   32803 B |        1.00 |
| EditList  | True   | 46,566.5 ns | 2,392.30 ns | 1,251.22 ns |  5.36 |    0.15 | 3.9063 | 3.4180 |   67215 B |        2.05 |


## MixedWide64

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method    | Retain | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|---------- |------- |-------------:|-------------:|-------------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
| **EditTree**  | **False**  |     **310.0 ns** |     **13.37 ns** |      **5.93 ns** | **0.010** |    **0.00** |  **0.1211** |  **0.0566** |       **-** |   **1.99 KB** |       **0.008** |
| EditArray | False  |  32,123.4 ns |  1,091.77 ns |    571.02 ns | 1.000 |    0.02 | 83.1163 | 83.1163 | 83.1163 | 256.07 KB |       1.000 |
| EditList  | False  |  16,531.2 ns |    850.49 ns |    444.82 ns | 0.515 |    0.02 | 10.9375 |  0.9766 |       - | 180.49 KB |       0.705 |
|           |        |              |              |              |       |         |         |         |         |           |             |
| **EditTree**  | **True**   |     **327.6 ns** |     **13.42 ns** |      **7.02 ns** | **0.006** |    **0.00** |  **0.1221** |  **0.0921** |       **-** |      **2 KB** |       **0.008** |
| EditArray | True   |  53,926.8 ns |  4,423.58 ns |  2,313.62 ns | 1.002 |    0.06 |  4.2969 |  4.2969 |  4.2969 | 256.05 KB |       1.000 |
| EditList  | True   | 144,434.1 ns | 21,444.67 ns | 11,215.98 ns | 2.683 |    0.22 | 14.6484 | 13.6719 |  4.8828 |  180.5 KB |       0.705 |


## PersistentPeerRead

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method            | N       | Edited | Mean         | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |-------- |------- |-------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
| **TreeFold**          | **65536**   | **False**  |     **24.66 μs** |     **0.200 μs** |   **0.089 μs** |  **0.06** |    **0.00** |         **-** |          **NA** |
| ImmutableListScan | 65536   | False  |    415.97 μs |     5.914 μs |   3.093 μs |  1.00 |    0.01 |         - |          NA |
|                   |         |        |              |              |            |       |         |           |             |
| **TreeFold**          | **65536**   | **True**   |     **51.91 μs** |     **2.712 μs** |   **1.418 μs** |  **0.13** |    **0.00** |         **-** |          **NA** |
| ImmutableListScan | 65536   | True   |    413.85 μs |     3.417 μs |   1.517 μs |  1.00 |    0.00 |         - |          NA |
|                   |         |        |              |              |            |       |         |           |             |
| **TreeFold**          | **1048576** | **False**  |    **410.43 μs** |    **14.987 μs** |   **7.839 μs** |  **0.02** |    **0.00** |         **-** |          **NA** |
| ImmutableListScan | 1048576 | False  | 17,006.62 μs | 1,204.679 μs | 534.885 μs |  1.00 |    0.04 |         - |          NA |
|                   |         |        |              |              |            |       |         |           |             |
| **TreeFold**          | **1048576** | **True**   |    **587.62 μs** |    **54.923 μs** |  **24.386 μs** |  **0.04** |    **0.00** |         **-** |          **NA** |
| ImmutableListScan | 1048576 | True   | 16,791.15 μs | 1,736.765 μs | 908.362 μs |  1.00 |    0.07 |         - |          NA |


## PersistentPeerEdits

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method             | Retain | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| **TreeEdits**          | **False**  | **225.4 ns** | **20.14 ns** | **10.53 ns** |  **1.75** |    **0.08** | **0.0407** | **0.0027** |     **689 B** |        **1.18** |
| ImmutableListEdits | False  | 128.7 ns |  3.44 ns |  1.80 ns |  1.00 |    0.02 | 0.0344 | 0.0050 |     583 B |        1.00 |
|                    |        |          |          |          |       |         |        |        |           |             |
| **TreeEdits**          | **True**   | **217.8 ns** |  **6.93 ns** |  **3.62 ns** |  **1.61** |    **0.03** | **0.0409** | **0.0155** |     **697 B** |        **1.18** |
| ImmutableListEdits | True   | 135.4 ns |  3.92 ns |  2.05 ns |  1.00 |    0.02 | 0.0353 | 0.0150 |     591 B |        1.00 |


## Additional public traversal API: ImmutableList.ForEach

The delegate is prepared outside the timed method. The million-element results have wide error intervals; both scan APIs are preserved rather than selecting an undocumented best-case path.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG
  Job-VKERRN : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

IterationCount=8  IterationTime=150ms  LaunchCount=1  
WarmupCount=3  

```
| Method               | N       | Edited | Mean        | Error        | StdDev      | Allocated |
|--------------------- |-------- |------- |------------:|-------------:|------------:|----------:|
| **ImmutableListForEach** | **65536**   | **False**  |    **404.0 μs** |      **1.61 μs** |     **0.72 μs** |         **-** |
| **ImmutableListForEach** | **65536**   | **True**   |    **402.8 μs** |      **2.31 μs** |     **1.03 μs** |         **-** |
| **ImmutableListForEach** | **1048576** | **False**  | **25,176.2 μs** | **12,261.19 μs** | **6,412.84 μs** |         **-** |
| **ImmutableListForEach** | **1048576** | **True**   | **24,328.8 μs** | **11,055.75 μs** | **5,782.37 μs** |         **-** |
