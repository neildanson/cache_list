# Persistent edit optimization benchmark details

Before and after runs on 19 October 2026. See [methodology and interpretation](edit-optimization.md). The boundary reports use the corrected, structurally verified fixture.

## Before

### Update

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------- |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeUpdate** | **1024**  | **151.4 ns** |  **6.08 ns** |  **2.70 ns** | **0.0394** |     **664 B** |
| **TreeUpdate** | **65536** | **354.4 ns** | **20.06 ns** | **10.49 ns** | **0.0728** |    **1232 B** |


### Append

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------- |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeAppend** | **1024**  | **219.1 ns** | **31.12 ns** | **16.28 ns** | **0.0853** |   **1.41 KB** |
| **TreeAppend** | **65536** | **432.0 ns** | **16.84 ns** |  **7.48 ns** | **0.1425** |   **2.36 KB** |


### Prepend

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method      | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------ |------ |---------:|---------:|---------:|-------:|----------:|
| **TreePrepend** | **1024**  | **220.0 ns** | **25.95 ns** | **13.57 ns** | **0.0856** |   **1.41 KB** |
| **TreePrepend** | **65536** | **429.9 ns** | **47.17 ns** | **20.94 ns** | **0.1430** |   **2.36 KB** |


### InsertMiddle

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method           | N     | Mean     | Error    | StdDev  | Gen0   | Allocated |
|----------------- |------ |---------:|---------:|--------:|-------:|----------:|
| **TreeInsertMiddle** | **1024**  | **214.0 ns** | **11.55 ns** | **6.04 ns** | **0.0852** |   **1.41 KB** |
| **TreeInsertMiddle** | **65536** | **423.6 ns** | **22.19 ns** | **9.85 ns** | **0.1433** |   **2.36 KB** |


### RemoveMiddle

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method           | N     | Mean     | Error    | StdDev  | Gen0   | Allocated |
|----------------- |------ |---------:|---------:|--------:|-------:|----------:|
| **TreeRemoveMiddle** | **1024**  | **137.5 ns** |  **8.29 ns** | **4.33 ns** | **0.0394** |     **664 B** |
| **TreeRemoveMiddle** | **65536** | **339.4 ns** | **16.59 ns** | **8.68 ns** | **0.0721** |    **1232 B** |


### RemoveLast

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method         | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|--------------- |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeRemoveLast** | **1024**  | **156.4 ns** | **40.98 ns** | **21.43 ns** | **0.0391** |     **664 B** |
| **TreeRemoveLast** | **65536** | **348.6 ns** | **22.59 ns** | **11.81 ns** | **0.0730** |    **1232 B** |


### Concat

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean      | Error     | StdDev    | Gen0   | Allocated |
|----------- |------ |----------:|----------:|----------:|-------:|----------:|
| **TreeConcat** | **1024**  | **212.56 ns** | **24.974 ns** | **11.089 ns** | **0.0944** |    **1592 B** |
| **TreeConcat** | **65536** |  **63.92 ns** |  **5.425 ns** |  **2.837 ns** | **0.0108** |     **184 B** |


### EditBoundaries

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method                  | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeRemoveMerge**         | **1024**  | **251.6 ns** | **10.43 ns** |  **5.46 ns** | **0.0701** |    **1176 B** |
| TreeRemoveRedistribute  | 1024  | 307.3 ns | 33.81 ns | 17.69 ns | 0.0903 |    1528 B |
| TreeInsertWithoutSplit  | 1024  | 135.8 ns |  7.45 ns |  3.90 ns | 0.0454 |     760 B |
| TreeConcatUnderfullRoot | 1024  | 216.2 ns | 16.27 ns |  8.51 ns | 0.0839 |    1408 B |
| **TreeRemoveMerge**         | **65536** | **643.9 ns** | **57.79 ns** | **30.22 ns** | **0.1514** |    **2544 B** |
| TreeRemoveRedistribute  | 65536 | 449.7 ns | 20.90 ns | 10.93 ns | 0.1259 |    2128 B |
| TreeInsertWithoutSplit  | 65536 | 315.4 ns | 34.25 ns | 17.91 ns | 0.0828 |    1392 B |
| TreeConcatUnderfullRoot | 65536 | 400.6 ns | 10.08 ns |  4.48 ns | 0.1424 |    2384 B |


## After

### Update

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean     | Error    | StdDev  | Gen0   | Allocated |
|----------- |------ |---------:|---------:|--------:|-------:|----------:|
| **TreeUpdate** | **1024**  | **104.7 ns** |  **2.55 ns** | **1.13 ns** | **0.0305** |     **512 B** |
| **TreeUpdate** | **65536** | **207.5 ns** | **12.68 ns** | **5.63 ns** | **0.0536** |     **896 B** |


### Append

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------- |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeAppend** | **1024**  | **173.0 ns** | **26.75 ns** | **13.99 ns** | **0.0569** |     **960 B** |
| **TreeAppend** | **65536** | **338.1 ns** | **62.93 ns** | **27.94 ns** | **0.0982** |    **1648 B** |


### Prepend

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method      | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------ |------ |---------:|---------:|---------:|-------:|----------:|
| **TreePrepend** | **1024**  | **190.5 ns** | **52.59 ns** | **27.50 ns** | **0.0572** |     **960 B** |
| **TreePrepend** | **65536** | **322.0 ns** | **11.86 ns** |  **5.27 ns** | **0.0972** |    **1648 B** |


### InsertMiddle

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method           | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------------- |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeInsertMiddle** | **1024**  | **143.9 ns** |  **9.28 ns** |  **4.85 ns** | **0.0571** |     **960 B** |
| **TreeInsertMiddle** | **65536** | **333.9 ns** | **24.29 ns** | **12.71 ns** | **0.0973** |    **1648 B** |


### RemoveMiddle

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method           | N     | Mean     | Error    | StdDev  | Gen0   | Allocated |
|----------------- |------ |---------:|---------:|--------:|-------:|----------:|
| **TreeRemoveMiddle** | **1024**  | **135.6 ns** | **11.52 ns** | **6.03 ns** | **0.0392** |     **664 B** |
| **TreeRemoveMiddle** | **65536** | **332.3 ns** |  **8.49 ns** | **3.03 ns** | **0.0723** |    **1232 B** |


### RemoveLast

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method         | N     | Mean     | Error    | StdDev  | Gen0   | Allocated |
|--------------- |------ |---------:|---------:|--------:|-------:|----------:|
| **TreeRemoveLast** | **1024**  | **138.6 ns** |  **8.59 ns** | **4.49 ns** | **0.0394** |     **664 B** |
| **TreeRemoveLast** | **65536** | **340.3 ns** | **13.59 ns** | **6.03 ns** | **0.0723** |    **1232 B** |


### Concat

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method     | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------- |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeConcat** | **1024**  | **33.81 ns** | **2.515 ns** | **0.897 ns** | **0.0076** |     **128 B** |
| **TreeConcat** | **65536** | **67.36 ns** | **8.796 ns** | **3.906 ns** | **0.0109** |     **184 B** |


### EditBoundaries

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
Unknown processor
.NET SDK 10.0.401
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3 DEBUG

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=8  
IterationTime=200ms  LaunchCount=1  WarmupCount=3  

```
| Method                  | N     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |------ |---------:|---------:|---------:|-------:|----------:|
| **TreeRemoveMerge**         | **1024**  | **195.3 ns** | **10.69 ns** |  **5.59 ns** | **0.0534** |     **904 B** |
| TreeRemoveRedistribute  | 1024  | 216.5 ns |  5.50 ns |  2.87 ns | 0.0615 |    1040 B |
| TreeInsertWithoutSplit  | 1024  | 112.0 ns | 10.32 ns |  4.58 ns | 0.0431 |     728 B |
| TreeConcatUnderfullRoot | 1024  | 156.7 ns |  8.70 ns |  4.55 ns | 0.0568 |     960 B |
| **TreeRemoveMerge**         | **65536** | **473.8 ns** | **35.47 ns** | **18.55 ns** | **0.1243** |    **2096 B** |
| TreeRemoveRedistribute  | 65536 | 388.8 ns | 21.70 ns | 11.35 ns | 0.0971 |    1640 B |
| TreeInsertWithoutSplit  | 65536 | 283.0 ns | 17.03 ns |  8.91 ns | 0.0809 |    1360 B |
| TreeConcatUnderfullRoot | 65536 | 327.7 ns | 20.72 ns |  9.20 ns | 0.0979 |    1648 B |

