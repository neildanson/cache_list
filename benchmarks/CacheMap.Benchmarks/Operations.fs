namespace CacheMap.Benchmarks

open BenchmarkDotNet.Attributes
open CacheCollections

// Each class is independently selectable. All input construction and probe
// selection happen in GlobalSetup, outside the measured operations.
[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type ContainsKey<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params(true,false)>]
    member val Hit = true with get,set
    member val Probes: 'K array = [||] with get,set
    [<GlobalSetup>]
    member this.Prepare() = this.Setup(); this.Probes <- if this.Hit then this.Hits else this.Misses
    [<Benchmark(OperationsPerInvoke=1024)>]
    member this.Packed() =
        let mutable found = 0
        for key in this.Probes do if CacheMap.containsKey key this.Tree then found <- found+1
        found
    [<Benchmark(Baseline=true,OperationsPerInvoke=1024)>]
    member this.FSharpMap() =
        let mutable found = 0
        for key in this.Probes do if Map.containsKey key this.Map then found <- found+1
        found

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type TryGetValue<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params(true,false)>]
    member val Hit = true with get,set
    member val Probes: 'K array = [||] with get,set
    [<GlobalSetup>]
    member this.Prepare() = this.Setup(); this.Probes <- if this.Hit then this.Hits else this.Misses
    [<Benchmark(OperationsPerInvoke=1024)>]
    member this.Packed() =
        let mutable total,value = 0,0
        for key in this.Probes do if this.Tree.TryGetValue(key,&value) then total <- total+value
        total
    [<Benchmark(Baseline=true,OperationsPerInvoke=1024)>]
    member this.FSharpMap() =
        let mutable total,value = 0,0
        for key in this.Probes do if this.Map.TryGetValue(key,&value) then total <- total+value
        total

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Find<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark(OperationsPerInvoke=1024)>]
    member this.Packed() =
        let mutable total = 0
        for key in this.Hits do total <- total + CacheMap.find key this.Tree
        total
    [<Benchmark(Baseline=true,OperationsPerInvoke=1024)>]
    member this.FSharpMap() =
        let mutable total = 0
        for key in this.Hits do total <- total + Map.find key this.Map
        total

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Change<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params("insert","update","remove","absent")>]
    member val Operation = "" with get,set
    member val Probes: 'K array = [||] with get,set
    member val Updater: int option -> int option = id with get,set
    [<GlobalSetup>]
    member this.Prepare() =
        this.Setup()
        this.Probes <- if this.Operation="insert" || this.Operation="absent" then this.Misses[..63] else this.Hits[..63]
        this.Updater <-
            match this.Operation with
            | "insert" -> fun _ -> Some -1
            | "update" -> Option.map ((+) 1)
            | "remove" | "absent" -> fun _ -> None
            | _ -> invalidOp "Unknown change operation"
    [<Benchmark(OperationsPerInvoke=64)>]
    member this.Packed() =
        Array.init 64 (fun i -> CacheMap.change this.Probes[i] this.Updater this.Tree)
    [<Benchmark(Baseline=true,OperationsPerInvoke=64)>]
    member this.FSharpMap() =
        Array.init 64 (fun i -> Map.change this.Probes[i] this.Updater this.Map)

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Partition<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params(0,50,100)>]
    member val Percent = 0 with get,set
    member val Predicate: 'K -> int -> bool = (fun _ _ -> false) with get,set
    [<GlobalSetup>]
    member this.Prepare() =
        this.Setup()
        let percent = this.Percent
        this.Predicate <- fun _ value -> value%100 < percent
    [<Benchmark>]
    member this.Packed() = CacheMap.partition this.Predicate this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = Map.partition this.Predicate this.Map

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Enumeration<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params("entries","tuples","keys","values")>]
    member val View = "" with get,set
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark>]
    member this.Packed() =
        let mutable total = 0L
        match this.View with
        | "entries" -> for entry in this.Tree do total <- total + int64 entry.Value
        | "tuples" -> for _,value in CacheMap.toSeq this.Tree do total <- total + int64 value
        | "keys" -> for key in CacheMap.keys this.Tree do total <- total + int64(Unchecked.hash key)
        | "values" -> for value in CacheMap.values this.Tree do total <- total + int64 value
        | _ -> invalidOp "Unknown view"
        total
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() =
        let mutable total = 0L
        match this.View with
        | "entries" -> for entry in this.Map do total <- total + int64 entry.Value
        | "tuples" -> for _,value in Map.toSeq this.Map do total <- total + int64 value
        | "keys" -> for key in Map.keys this.Map do total <- total + int64(Unchecked.hash key)
        | "values" -> for value in Map.values this.Map do total <- total + int64 value
        | _ -> invalidOp "Unknown view"
        total

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type ToArray<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark>]
    member this.Packed() = CacheMap.toArray this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = Map.toArray this.Map

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type ToList<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark>]
    member this.Packed() = CacheMap.toList this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = Map.toList this.Map

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Search<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params("first","middle","last","missing")>]
    member val Position = "" with get,set
    member val Chooser: 'K -> int -> int option = (fun _ _ -> None) with get,set
    [<GlobalSetup>]
    member this.Prepare() =
        this.Setup()
        let target =
            match this.Position with
            | "first" -> 0 | "middle" -> this.N/2 | "last" -> this.N-1 | "missing" -> -1
            | _ -> invalidOp "Unknown search position"
        this.Chooser <- fun _ value -> if value=target then Some value else None
    [<Benchmark>]
    member this.Packed() = CacheMap.tryPick this.Chooser this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = Map.tryPick this.Chooser this.Map

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type FoldBack<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark>]
    member this.Packed() = CacheMap.foldBack (fun _ value state -> state+int64 value) this.Tree 0L
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = Map.foldBack (fun _ value state -> state+int64 value) this.Map 0L

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Iteration<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark>]
    member this.Packed() =
        let mutable total = 0L
        CacheMap.iter (fun _ value -> total <- total+int64 value) this.Tree
        total
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() =
        let mutable total = 0L
        Map.iter (fun _ value -> total <- total+int64 value) this.Map
        total

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Extremes<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params("min","max")>]
    member val End = "" with get,set
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark>]
    member this.Packed() = if this.End="min" then CacheMap.minKeyValue this.Tree else CacheMap.maxKeyValue this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = if this.End="min" then Map.minKeyValue this.Map else Map.maxKeyValue this.Map

module OperationValidation =
    let private same label expected actual =
        if expected<>actual then failwithf "%s result mismatch" label
    let runFor<'K when 'K: comparison> () =
        for n in [1024;65536] do
            for hit in [true;false] do
                let contains = ContainsKey<'K>(N=n,Hit=hit)
                contains.Prepare()
                same "ContainsKey" (if hit then 1024 else 0) (contains.Packed())
                same "ContainsKey baseline" (contains.FSharpMap()) (contains.Packed())
                let lookup = TryGetValue<'K>(N=n,Hit=hit)
                lookup.Prepare()
                same "TryGetValue" (lookup.FSharpMap()) (lookup.Packed())
            let find = Find<'K>(N=n)
            find.Prepare()
            same "Find" (find.FSharpMap()) (find.Packed())
            for op in ["insert";"update";"remove";"absent"] do
                let change = Change<'K>(N=n,Operation=op)
                change.Prepare()
                Array.iter2 (fun t m -> same "Change" (Map.toArray m) (CacheMap.toArray t)) (change.Packed()) (change.FSharpMap())
                // The original roots must still contain precisely the input.
                same "Change source" change.Entries (CacheMap.toArray change.Tree)
            for percent in [0;50;100] do
                let partition = Partition<'K>(N=n,Percent=percent)
                partition.Prepare()
                let a,b = partition.Packed()
                let c,d = partition.FSharpMap()
                same "Partition left" (Map.toArray c) (CacheMap.toArray a)
                same "Partition right" (Map.toArray d) (CacheMap.toArray b)
            for view in ["entries";"tuples";"keys";"values"] do
                let enumeration = Enumeration<'K>(N=n,View=view)
                enumeration.Prepare()
                same "Enumeration" (enumeration.FSharpMap()) (enumeration.Packed())
            let array = ToArray<'K>(N=n)
            array.Prepare()
            same "ToArray" array.Entries (array.Packed())
            same "ToArray baseline" (array.FSharpMap()) (array.Packed())
            let list = ToList<'K>(N=n)
            list.Prepare()
            same "ToList" (list.FSharpMap()) (list.Packed())
            for position in ["first";"middle";"last";"missing"] do
                let search = Search<'K>(N=n,Position=position)
                search.Prepare()
                let expected =
                    match position with
                    | "first" -> Some 0 | "middle" -> Some(n/2) | "last" -> Some(n-1) | _ -> None
                same "Search" expected (search.Packed())
                same "Search baseline" (search.FSharpMap()) (search.Packed())
            let fold = FoldBack<'K>(N=n)
            fold.Prepare()
            let expectedSum = int64 n * int64(n-1)/2L
            same "FoldBack" expectedSum (fold.Packed())
            same "FoldBack baseline" (fold.FSharpMap()) (fold.Packed())
            let iter = Iteration<'K>(N=n)
            iter.Prepare()
            same "Iteration" expectedSum (iter.Packed())
            same "Iteration baseline" (iter.FSharpMap()) (iter.Packed())
            for ending in ["min";"max"] do
                let extremes = Extremes<'K>(N=n,End=ending)
                extremes.Prepare()
                same "Extremes" (extremes.FSharpMap()) (extremes.Packed())
    let run () = runFor<int>(); runFor<string>()
