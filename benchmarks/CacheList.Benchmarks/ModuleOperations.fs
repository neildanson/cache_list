namespace CacheList.Benchmarks

open BenchmarkDotNet.Attributes
open CacheCollections

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type Map() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeMap() = CacheList.map (fun x -> x + 1) this.Tree

    [<Benchmark>]
    member this.ArrayMap() = Array.map (fun x -> x + 1) this.Array

    [<Benchmark>]
    member this.ListMap() = List.map (fun x -> x + 1) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type Mapi() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeMapi() = CacheList.mapi (fun i x -> i + x) this.Tree

    [<Benchmark>]
    member this.ArrayMapi() = Array.mapi (fun i x -> i + x) this.Array

    [<Benchmark>]
    member this.ListMapi() = List.mapi (fun i x -> i + x) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type Filter() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeFilter() = CacheList.filter (fun x -> x % 2 = 0) this.Tree

    [<Benchmark>]
    member this.ArrayFilter() = Array.filter (fun x -> x % 2 = 0) this.Array

    [<Benchmark>]
    member this.ListFilter() = List.filter (fun x -> x % 2 = 0) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type Choose() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeChoose() = CacheList.choose (fun x -> if x % 2 = 0 then Some (x + 1) else None) this.Tree

    [<Benchmark>]
    member this.ArrayChoose() = Array.choose (fun x -> if x % 2 = 0 then Some (x + 1) else None) this.Array

    [<Benchmark>]
    member this.ListChoose() = List.choose (fun x -> if x % 2 = 0 then Some (x + 1) else None) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type Partition() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreePartition() = CacheList.partition (fun x -> x % 2 = 0) this.Tree

    [<Benchmark>]
    member this.ArrayPartition() = Array.partition (fun x -> x % 2 = 0) this.Array

    [<Benchmark>]
    member this.ListPartition() = List.partition (fun x -> x % 2 = 0) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type Reverse() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeReverse() = CacheList.rev this.Tree

    [<Benchmark>]
    member this.ArrayReverse() = Array.rev this.Array

    [<Benchmark>]
    member this.ListReverse() = List.rev this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type SortBy() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeSortBy() = CacheList.sortBy (fun x -> -x) this.Tree

    [<Benchmark>]
    member this.ArraySortBy() = Array.sortBy (fun x -> -x) this.Array

    [<Benchmark>]
    member this.ListSortBy() = List.sortBy (fun x -> -x) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type DistinctBy() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeDistinctBy() = CacheList.distinctBy (fun x -> x % 128) this.Tree

    [<Benchmark>]
    member this.ArrayDistinctBy() = Array.distinctBy (fun x -> x % 128) this.Array

    [<Benchmark>]
    member this.ListDistinctBy() = List.distinctBy (fun x -> x % 128) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type GroupBy() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeGroupBy() = CacheList.groupBy (fun x -> x % 128) this.Tree

    [<Benchmark>]
    member this.ArrayGroupBy() = Array.groupBy (fun x -> x % 128) this.Array

    [<Benchmark>]
    member this.ListGroupBy() = List.groupBy (fun x -> x % 128) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type CountBy() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeCountBy() = CacheList.countBy (fun x -> x % 128) this.Tree

    [<Benchmark>]
    member this.ArrayCountBy() = Array.countBy (fun x -> x % 128) this.Array

    [<Benchmark>]
    member this.ListCountBy() = List.countBy (fun x -> x % 128) this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type Scan() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeScan() = CacheList.scan (fun state x -> state + int64 x) 0L this.Tree

    [<Benchmark>]
    member this.ArrayScan() = Array.scan (fun state x -> state + int64 x) 0L this.Array

    [<Benchmark>]
    member this.ListScan() = List.scan (fun state x -> state + int64 x) 0L this.List

[<MemoryDiagnoser; BenchmarkCategory("Module")>]
type SumBy() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeSumBy() = CacheList.sumBy (fun x -> int64 x) this.Tree

    [<Benchmark>]
    member this.ArraySumBy() = Array.sumBy (fun x -> int64 x) this.Array

    [<Benchmark>]
    member this.ListSumBy() = List.sumBy (fun x -> int64 x) this.List
