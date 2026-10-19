# Module optimization benchmark details

Before and after short in-process runs, 19 October 2026. See [methodology and interpretation](module-optimization.md).

## Before

### Map

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=5  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method  | N     | Mean       | Error      | StdDev    | Gen0     | Gen1     | Gen2     | Allocated |
|-------- |------ |-----------:|-----------:|----------:|---------:|---------:|---------:|----------:|
| **TreeMap** | **1024**  |   **8.742 μs** |  **0.4514 μs** | **0.0699 μs** |   **0.9014** |        **-** |        **-** |  **15.61 KB** |
| **TreeMap** | **65536** | **858.408 μs** | **23.3333 μs** | **6.0596 μs** | **120.8333** | **120.8333** | **120.8333** | **929.53 KB** |


### Filter

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=5  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean       | Error      | StdDev    | Gen0    | Gen1    | Gen2    | Allocated |
|----------- |------ |-----------:|-----------:|----------:|--------:|--------:|--------:|----------:|
| **TreeFilter** | **1024**  |   **5.231 μs** |  **0.4297 μs** | **0.1116 μs** |  **0.4944** |       **-** |       **-** |   **8.41 KB** |
| **TreeFilter** | **65536** | **451.845 μs** | **43.4881 μs** | **6.7298 μs** | **40.9483** | **40.9483** | **40.9483** | **465.52 KB** |


### CountBy

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=5  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method      | N     | Mean      | Error     | StdDev    | Gen0     | Gen1    | Allocated  |
|------------ |------ |----------:|----------:|----------:|---------:|--------:|-----------:|
| **TreeCountBy** | **1024**  |  **14.31 μs** |  **1.859 μs** |  **0.483 μs** |   **3.0069** |  **0.2148** |   **49.45 KB** |
| **TreeCountBy** | **65536** | **830.33 μs** | **65.412 μs** | **10.123 μs** | **125.0000** | **93.7500** | **2065.99 KB** |


## After

### Map

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=5  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method  | N     | Mean        | Error       | StdDev     | Gen0    | Gen1   | Allocated |
|-------- |------ |------------:|------------:|-----------:|--------:|-------:|----------:|
| **TreeMap** | **1024**  |    **933.5 ns** |    **467.2 ns** |   **121.3 ns** |  **0.3516** | **0.0053** |   **5.83 KB** |
| **TreeMap** | **65536** | **67,088.3 ns** | **27,930.6 ns** | **7,253.5 ns** | **22.7273** | **8.3333** | **372.21 KB** |


### Filter

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=5  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean       | Error      | StdDev     | Gen0    | Gen1   | Allocated |
|----------- |------ |-----------:|-----------:|-----------:|--------:|-------:|----------:|
| **TreeFilter** | **1024**  |   **5.259 μs** |   **2.558 μs** |  **0.6643 μs** |  **0.2151** |      **-** |   **3.66 KB** |
| **TreeFilter** | **65536** | **343.928 μs** | **182.894 μs** | **47.4969 μs** | **11.7188** | **1.9531** | **215.95 KB** |


### CountBy

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=5  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method      | N     | Mean      | Error      | StdDev    | Gen0   | Allocated |
|------------ |------ |----------:|-----------:|----------:|-------:|----------:|
| **TreeCountBy** | **1024**  |  **12.73 μs** |   **6.687 μs** |  **1.737 μs** | **1.1742** |  **19.67 KB** |
| **TreeCountBy** | **65536** | **650.44 μs** | **157.043 μs** | **24.303 μs** |      **-** |  **19.68 KB** |

