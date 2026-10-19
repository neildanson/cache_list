namespace CacheMap.Benchmarks

open System
open System.Collections.Generic
open System.Text.Json
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open CacheCollections

module Workload =
    let key<'K> i : 'K =
        if typeof<'K> = typeof<int> then unbox(box i)
        elif typeof<'K> = typeof<string> then unbox(box (sprintf "key-%010d" i))
        else invalidOp "Unsupported benchmark key type"
    let shuffle (source: 'T array) =
        let result = Array.copy source
        let random = Random 431
        random.Shuffle result
        result
    let entries<'K> n = Array.init n (fun i -> key<'K> (2*i),i)
    // Updates hit existing even keys; inserts target absent odd keys, then get
    // removed. Each complete triple restores the original key set and count.
    let trace<'K> n steps =
        let random = Random 917
        Array.init steps (fun i ->
            let ordinal = random.Next n
            let k = if i%3=0 then 2*ordinal else 2*((i/3)%n)+1
            struct(i%3,key<'K> k,-i-1))
    let editTree source struct(operation,key,value) =
        if operation=2 then CacheMap.remove key source else CacheMap.add key value source
    let editMap source struct(operation,key,value) =
        if operation=2 then Map.remove key source else Map.add key value source
    let history edit source (trace: 'Edit array) retain =
        let kept = Array.zeroCreate (if retain then trace.Length+1 else 1)
        let mutable current = source
        if retain then kept[0] <- current
        for i=0 to trace.Length-1 do
            current <- edit current trace[i]
            if retain then kept[i+1] <- current
        if not retain then kept[0] <- current
        kept

type Input<'K when 'K: comparison>() =
    [<Params(1024,65536)>]
    member val N = 0 with get,set
    member val Tree = CacheMap.empty<'K,int> with get,set
    member val Map = Map.empty<'K,int> with get,set
    member val Entries: ('K*int) array = [||] with get,set
    member val Hits: 'K array = [||] with get,set
    member val Misses: 'K array = [||] with get,set
    member val Trace: struct(int*'K*int) array = [||] with get,set
    member this.Setup() =
        // Build through shuffled inserts, rather than giving only CacheMap a
        // perfectly packed bulk shape. Build benchmarks explicitly vary order.
        this.Entries <- Workload.entries<'K> this.N
        let shuffled = Workload.shuffle this.Entries
        this.Tree <- Array.fold(fun m (k,v) -> CacheMap.add k v m) CacheMap.empty shuffled
        this.Map <- Map.ofArray shuffled
        let random = Random 879
        let indices = Array.init 1024 (fun _ -> random.Next this.N)
        this.Hits <- indices |> Array.map(fun i -> Workload.key<'K> (2*i))
        this.Misses <- indices |> Array.map(fun i -> Workload.key<'K> (2*i+1))
        this.Trace <- Workload.trace<'K> this.N 255

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Lookup<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params(true,false)>]
    member val Hit = true with get,set
    member val Probes: 'K array = [||] with get,set
    [<GlobalSetup>]
    member this.Prepare() = this.Setup(); this.Probes <- if this.Hit then this.Hits else this.Misses
    [<Benchmark(OperationsPerInvoke=1024)>]
    member this.Packed() =
        let mutable total = 0
        for k in this.Probes do
            match CacheMap.tryFind k this.Tree with Some value -> total <- total+value | None -> ()
        total
    [<Benchmark(Baseline=true,OperationsPerInvoke=1024)>]
    member this.FSharpMap() =
        let mutable total = 0
        for k in this.Probes do
            match Map.tryFind k this.Map with Some value -> total <- total+value | None -> ()
        total

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Edit<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params("insert","replace","remove","absent")>]
    member val Operation = "" with get,set
    member this.IsAdd = this.Operation="insert" || this.Operation="replace"
    member val Probes: 'K array = [||] with get,set
    [<GlobalSetup>]
    member this.Prepare() =
        this.Setup()
        this.Probes <- if this.Operation="insert" || this.Operation="absent" then this.Misses[..63] else this.Hits[..63]
    [<Benchmark(OperationsPerInvoke=64)>]
    member this.Packed() =
        let results = Array.zeroCreate 64
        let source = this.Tree
        if this.IsAdd then for i=0 to 63 do results[i] <- CacheMap.add this.Probes[i] -1 source
        else for i=0 to 63 do results[i] <- CacheMap.remove this.Probes[i] source
        results
    [<Benchmark(Baseline=true,OperationsPerInvoke=64)>]
    member this.FSharpMap() =
        let results = Array.zeroCreate 64
        let source = this.Map
        if this.IsAdd then for i=0 to 63 do results[i] <- Map.add this.Probes[i] -1 source
        else for i=0 to 63 do results[i] <- Map.remove this.Probes[i] source
        results

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Build<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<Params(true,false)>]
    member val Sorted = false with get,set
    member val Data: ('K*int) array = [||] with get,set
    [<GlobalSetup>]
    member this.Prepare() =
        this.Setup()
        this.Data <- if this.Sorted then this.Entries else Workload.shuffle this.Entries
    [<Benchmark>]
    member this.Packed() = CacheMap.ofArray this.Data
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = Map.ofArray this.Data

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Fold<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Benchmark>]
    member this.Packed() = CacheMap.fold (fun s _ v -> s+int64 v) 0L this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() = Map.fold (fun s _ v -> s+int64 v) 0L this.Map

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Transform<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Params("map","filter")>]
    member val Operation = "" with get,set
    [<Benchmark>]
    member this.Packed() =
        if this.Operation="map" then CacheMap.map (fun _ v -> v+1) this.Tree
        else CacheMap.filter (fun _ v -> v%2=0) this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FSharpMap() =
        if this.Operation="map" then Map.map (fun _ v -> v+1) this.Map
        else Map.filter (fun _ v -> v%2=0) this.Map

[<MemoryDiagnoser;GenericTypeArguments(typeof<int>);GenericTypeArguments(typeof<string>)>]
type Mixed<'K when 'K: comparison>() =
    inherit Input<'K>()
    [<GlobalSetup>]
    member this.Prepare() = this.Setup()
    [<Params(true,false)>]
    member val Retain = false with get,set
    [<Benchmark(OperationsPerInvoke=255)>]
    member this.Packed() = Workload.history Workload.editTree this.Tree this.Trace this.Retain
    [<Benchmark(Baseline=true,OperationsPerInvoke=255)>]
    member this.FSharpMap() = Workload.history Workload.editMap this.Map this.Trace this.Retain

module Validation =
    let runFor<'K when 'K: comparison> () =
        for n in [1024;65536] do
            for hit in [false;true] do
                let b = Lookup<'K>(N=n,Hit=hit)
                b.Prepare()
                if b.Packed()<>b.FSharpMap() then failwith "Lookup mismatch"
            for op in ["insert";"replace";"remove";"absent"] do
                let b = Edit<'K>(N=n,Operation=op)
                b.Prepare()
                Array.iter2(fun t m -> if CacheMap.toArray t<>Map.toArray m then failwith "Edit mismatch") (b.Packed()) (b.FSharpMap())
            for sorted in [false;true] do
                let b = Build<'K>(N=n,Sorted=sorted)
                b.Prepare()
                if CacheMap.toArray(b.Packed())<>Map.toArray(b.FSharpMap()) then failwith "Build mismatch"
            let fold = Fold<'K>(N=n)
            fold.Setup()
            if fold.Packed()<>fold.FSharpMap() then failwith "Fold mismatch"
            for op in ["map";"filter"] do
                let b = Transform<'K>(N=n,Operation=op)
                b.Setup()
                if CacheMap.toArray(b.Packed())<>Map.toArray(b.FSharpMap()) then failwith "Transform mismatch"
            for retain in [false;true] do
                let b = Mixed<'K>(N=n,Retain=retain)
                b.Setup()
                Array.iter2(fun t m -> if CacheMap.toArray t<>Map.toArray m then failwith "History mismatch") (b.Packed()) (b.FSharpMap())
    let run () = runFor<int>(); runFor<string>(); printfn "All benchmark outputs and retained versions match F# Map."

module Retention =
    let runFor<'K when 'K: comparison> implementation n steps =
        let entries = Workload.entries<'K> n |> Workload.shuffle
        let trace = Workload.trace<'K> n steps
        // Both baseline collections and their input payloads survive both samples.
        let tree = Array.fold(fun m (k,v) -> CacheMap.add k v m) CacheMap.empty entries
        let map = Map.ofArray entries
        // JIT before sampling, with independent roots released by returning.
        let warm () =
            Workload.history Workload.editTree tree trace false |> ignore
            Workload.history Workload.editMap map trace false |> ignore
        warm()
        let baseline = GC.GetTotalMemory(true)
        let allocated = GC.GetTotalAllocatedBytes(true)
        let roots: obj =
            if implementation="packed" then box(Workload.history Workload.editTree tree trace true)
            elif implementation="map" then box(Workload.history Workload.editMap map trace true)
            else invalidArg "implementation" "Use packed or map"
        let allocated = GC.GetTotalAllocatedBytes(true)-allocated
        let live = GC.GetTotalMemory(true)-baseline
        // Validate every retained root against both an independent mutable
        // dictionary and the other persistent implementation after sampling.
        let model = SortedDictionary<'K,int>(LanguagePrimitives.FastGenericComparer<'K>)
        for k,v in entries do model.Add(k,v)
        let verify i =
            let actual =
                if implementation="packed" then CacheMap.toArray ((unbox<CacheMap<'K,int> array> roots)[i])
                else Map.toArray ((unbox<Map<'K,int> array> roots)[i])
            if actual.Length<>model.Count then failwith "Retained count mismatch"
            use mutable iterator = model.GetEnumerator()
            for k,v in actual do
                if not(iterator.MoveNext()) || iterator.Current.Key<>k || iterator.Current.Value<>v then failwith "Retained content mismatch"
        verify 0
        for i=0 to trace.Length-1 do
            let struct(op,k,v) = trace[i]
            if op=2 then model.Remove k |> ignore else model[k] <- v
            verify (i+1)
        GC.KeepAlive roots; GC.KeepAlive tree; GC.KeepAlive map; GC.KeepAlive entries; GC.KeepAlive trace
        printfn "%s" (JsonSerializer.Serialize {|keyType=typeof<'K>.Name;implementation=implementation;n=n;steps=steps;roots=steps+1;liveBytes=live;allocatedBytes=allocated;validated=true|})
    let run payload implementation n steps =
        if payload="int" then runFor<int> implementation n steps
        elif payload="string" then runFor<string> implementation n steps
        else invalidArg "payload" "Use int or string"

module Program =
    [<EntryPoint>]
    let main args =
        match args with
        | [|"--validate-only"|] -> Validation.run(); 0
        | [|"--retention";payload;implementation;n;steps|] -> Retention.run payload implementation (int n) (int steps); 0
        | _ -> BenchmarkSwitcher.FromAssembly(typeof<Input<int>>.Assembly).Run(args) |> ignore; 0
