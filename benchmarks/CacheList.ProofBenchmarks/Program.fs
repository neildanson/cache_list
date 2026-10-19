namespace CacheList.ProofBenchmarks

open System
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open CacheCollections

// Deliberately use statically typed scan loops for each payload: no per-item
// projection callback or boxing is introduced by a shared benchmark helper.

type ReadIntInput() =
    let mutable tree = CacheList.empty<int>
    let mutable array: int array = [||]
    let mutable list: int list = []
    [<Params(65536, 1048576, 4194304)>]
    member val N = 0 with get,set
    [<Params(false,true)>]
    member val Edited = false with get,set
    [<GlobalSetup>]
    member this.Setup() =
        let t,a,l = Workload.readInput id this.N this.Edited
        tree <- t
        array <- a
        list <- l
        Workload.verifyTree tree
        let expected = Array.sumBy (fun (value:int) -> int64 value) array
        if CacheList.sumBy (fun (value:int) -> int64 value) tree <> expected || List.sumBy (fun (value:int) -> int64 value) list <> expected then
            failwith "Read input mismatch"
    member _.Tree = tree
    member _.Array = array
    member _.List = list

[<MemoryDiagnoser>]
type TraverseInt() =
    inherit ReadIntInput()
    [<Benchmark>]
    member this.ScanTree() =
        let rec visit (node: Node<int>) =
            match node with
            | Leaf values ->
                let mutable total = 0L
                for value in values do total <- total + int64 value
                total
            | Branch(children,_) ->
                let mutable total = 0L
                for child in children do total <- total + visit child
                total
        visit this.Tree.Root
    [<Benchmark(Baseline=true)>]
    member this.ScanArray() =
        let mutable total = 0L
        for value in this.Array do total <- total + int64 value
        total
    [<Benchmark>]
    member this.ScanList() =
        let mutable total = 0L
        let mutable rest = this.List
        while not (List.isEmpty rest) do
            let value = List.head rest
            total <- total + int64 value
            rest <- List.tail rest
        total

type ReadReferenceInput() =
    let mutable tree = CacheList.empty<ReferenceValue>
    let mutable array: ReferenceValue array = [||]
    let mutable list: ReferenceValue list = []
    [<Params(65536, 1048576)>]
    member val N = 0 with get,set
    [<Params(false,true)>]
    member val Edited = false with get,set
    [<GlobalSetup>]
    member this.Setup() =
        let t,a,l = Workload.readInput ReferenceValue this.N this.Edited
        tree <- t
        array <- a
        list <- l
        Workload.verifyTree tree
        let expected = Array.sumBy (fun (value:ReferenceValue) -> value.A) array
        if CacheList.sumBy (fun (value:ReferenceValue) -> value.A) tree <> expected || List.sumBy (fun (value:ReferenceValue) -> value.A) list <> expected then
            failwith "Read input mismatch"
    member _.Tree = tree
    member _.Array = array
    member _.List = list

[<MemoryDiagnoser>]
type TraverseReference() =
    inherit ReadReferenceInput()
    [<Benchmark>]
    member this.ScanTree() =
        let rec visit (node: Node<ReferenceValue>) =
            match node with
            | Leaf values ->
                let mutable total = 0L
                for value in values do total <- total + value.A
                total
            | Branch(children,_) ->
                let mutable total = 0L
                for child in children do total <- total + visit child
                total
        visit this.Tree.Root
    [<Benchmark(Baseline=true)>]
    member this.ScanArray() =
        let mutable total = 0L
        for value in this.Array do total <- total + value.A
        total
    [<Benchmark>]
    member this.ScanList() =
        let mutable total = 0L
        let mutable rest = this.List
        while not (List.isEmpty rest) do
            let value = List.head rest
            total <- total + value.A
            rest <- List.tail rest
        total

type ReadWide64Input() =
    let mutable tree = CacheList.empty<Wide64>
    let mutable array: Wide64 array = [||]
    let mutable list: Wide64 list = []
    [<Params(65536, 1048576)>]
    member val N = 0 with get,set
    [<Params(false,true)>]
    member val Edited = false with get,set
    [<GlobalSetup>]
    member this.Setup() =
        let t,a,l = Workload.readInput Wide64 this.N this.Edited
        tree <- t
        array <- a
        list <- l
        Workload.verifyTree tree
        let expected = Array.sumBy (fun (value:Wide64) -> value.A) array
        if CacheList.sumBy (fun (value:Wide64) -> value.A) tree <> expected || List.sumBy (fun (value:Wide64) -> value.A) list <> expected then
            failwith "Read input mismatch"
    member _.Tree = tree
    member _.Array = array
    member _.List = list

[<MemoryDiagnoser>]
type TraverseWide64() =
    inherit ReadWide64Input()
    [<Benchmark>]
    member this.ScanTree() =
        let rec visit (node: Node<Wide64>) =
            match node with
            | Leaf values ->
                let mutable total = 0L
                for value in values do total <- total + value.A
                total
            | Branch(children,_) ->
                let mutable total = 0L
                for child in children do total <- total + visit child
                total
        visit this.Tree.Root
    [<Benchmark(Baseline=true)>]
    member this.ScanArray() =
        let mutable total = 0L
        for value in this.Array do total <- total + value.A
        total
    [<Benchmark>]
    member this.ScanList() =
        let mutable total = 0L
        let mutable rest = this.List
        while not (List.isEmpty rest) do
            let value = List.head rest
            total <- total + value.A
            rest <- List.tail rest
        total

[<MemoryDiagnoser>]
type PublicFoldInt() =
    inherit ReadIntInput()
    [<Benchmark>]
    member this.FoldTree() = CacheList.fold (fun sum value -> sum + int64 value) 0L this.Tree
    [<Benchmark(Baseline=true)>]
    member this.FoldArray() = Array.fold (fun sum value -> sum + int64 value) 0L this.Array
    [<Benchmark>]
    member this.FoldList() = List.fold (fun sum value -> sum + int64 value) 0L this.List

[<MemoryDiagnoser>]
type MixedInt() =
    let mutable tree = CacheList.empty<int>
    let mutable array: int array = [||]
    let mutable list: int list = []
    let mutable edits: Edit<int> array = [||]
    [<Params(false,true)>]
    member val Retain = false with get,set
    [<GlobalSetup>]
    member _.Setup() =
        let t,a,l = Workload.readInput id 4096 true
        tree <- t
        array <- a
        list <- l
        edits <- Workload.prepareEdits id a.Length Workload.steps
    [<Benchmark(OperationsPerInvoke=256)>]
    member this.EditTree() = Workload.history Workload.treeEdit tree edits this.Retain
    [<Benchmark(Baseline=true, OperationsPerInvoke=256)>]
    member this.EditArray() = Workload.history Workload.arrayEdit array edits this.Retain
    [<Benchmark(OperationsPerInvoke=256)>]
    member this.EditList() = Workload.history Workload.listEdit list edits this.Retain

[<MemoryDiagnoser>]
type MixedReference() =
    let mutable tree = CacheList.empty<ReferenceValue>
    let mutable array: ReferenceValue array = [||]
    let mutable list: ReferenceValue list = []
    let mutable edits: Edit<ReferenceValue> array = [||]
    [<Params(false,true)>]
    member val Retain = false with get,set
    [<GlobalSetup>]
    member _.Setup() =
        let t,a,l = Workload.readInput ReferenceValue 4096 true
        tree <- t
        array <- a
        list <- l
        edits <- Workload.prepareEdits ReferenceValue a.Length Workload.steps
    [<Benchmark(OperationsPerInvoke=256)>]
    member this.EditTree() = Workload.history Workload.treeEdit tree edits this.Retain
    [<Benchmark(Baseline=true, OperationsPerInvoke=256)>]
    member this.EditArray() = Workload.history Workload.arrayEdit array edits this.Retain
    [<Benchmark(OperationsPerInvoke=256)>]
    member this.EditList() = Workload.history Workload.listEdit list edits this.Retain

[<MemoryDiagnoser>]
type MixedWide64() =
    let mutable tree = CacheList.empty<Wide64>
    let mutable array: Wide64 array = [||]
    let mutable list: Wide64 list = []
    let mutable edits: Edit<Wide64> array = [||]
    [<Params(false,true)>]
    member val Retain = false with get,set
    [<GlobalSetup>]
    member _.Setup() =
        let t,a,l = Workload.readInput Wide64 4096 true
        tree <- t
        array <- a
        list <- l
        edits <- Workload.prepareEdits Wide64 a.Length Workload.steps
    [<Benchmark(OperationsPerInvoke=256)>]
    member this.EditTree() = Workload.history Workload.treeEdit tree edits this.Retain
    [<Benchmark(Baseline=true, OperationsPerInvoke=256)>]
    member this.EditArray() = Workload.history Workload.arrayEdit array edits this.Retain
    [<Benchmark(OperationsPerInvoke=256)>]
    member this.EditList() = Workload.history Workload.listEdit list edits this.Retain


[<MemoryDiagnoser>]
type PersistentPeerRead() =
    let mutable tree = CacheList.empty<int>
    let mutable peer = System.Collections.Immutable.ImmutableList<int>.Empty
    let mutable sum = 0L
    let accumulate = Action<int>(fun value -> sum <- sum + int64 value)
    [<Params(65536,1048576)>]
    member val N = 0 with get,set
    [<Params(false,true)>]
    member val Edited = false with get,set
    [<GlobalSetup>]
    member this.Setup() =
        let t,values,_ = Workload.readInput id this.N this.Edited
        tree <- t
        peer <- System.Collections.Immutable.ImmutableList.CreateRange values
    [<Benchmark>]
    member _.TreeFold() = CacheList.fold (fun s value -> s+int64 value) 0L tree
    [<Benchmark(Baseline=true)>]
    member _.ImmutableListScan() =
        let mutable total = 0L
        let mutable cursor = peer.GetEnumerator()
        try
            while cursor.MoveNext() do total <- total + int64 cursor.Current
        finally cursor.Dispose()
        total
    [<Benchmark>]
    member _.ImmutableListForEach() =
        sum <- 0L
        peer.ForEach accumulate
        sum

[<MemoryDiagnoser>]
type PersistentPeerEdits() =
    let mutable tree = CacheList.empty<int>
    let mutable peer = System.Collections.Immutable.ImmutableList<int>.Empty
    let mutable edits = [||]
    [<Params(false,true)>]
    member val Retain = false with get,set
    [<GlobalSetup>]
    member _.Setup() =
        let t,values,_ = Workload.readInput id 4096 true
        tree <- t
        peer <- System.Collections.Immutable.ImmutableList.CreateRange values
        edits <- Workload.prepareEdits id values.Length Workload.steps
    [<Benchmark(OperationsPerInvoke=256)>]
    member this.TreeEdits() = Workload.history Workload.treeEdit tree edits this.Retain
    [<Benchmark(Baseline=true,OperationsPerInvoke=256)>]
    member this.ImmutableListEdits() = Workload.history Workload.immutableEdit peer edits this.Retain

module BenchmarkValidation =
    let private checkHistories (trees: CacheList<'T> array) (arrays: 'T array array) (lists: 'T list array) =
        if trees.Length<>arrays.Length || trees.Length<>lists.Length then failwith "Wrong retained root count"
        for i=0 to trees.Length-1 do
            if CacheList.toArray trees[i]<>arrays[i] || List.toArray lists[i]<>arrays[i] then
                failwith "Measured history result mismatch"
            Workload.verifyTree trees[i]
    let run () =
        Workload.validateAll()
        for edited in [false;true] do
            let ints = TraverseInt(N=65536,Edited=edited)
            ints.Setup()
            if ints.ScanTree()<>ints.ScanArray() || ints.ScanList()<>ints.ScanArray() then failwith "Integer scan mismatch"
            let refs = TraverseReference(N=65536,Edited=edited)
            refs.Setup()
            if refs.ScanTree()<>refs.ScanArray() || refs.ScanList()<>refs.ScanArray() then failwith "Reference scan mismatch"
            let wide = TraverseWide64(N=65536,Edited=edited)
            wide.Setup()
            if wide.ScanTree()<>wide.ScanArray() || wide.ScanList()<>wide.ScanArray() then failwith "Wide scan mismatch"
            let folds = PublicFoldInt(N=65536,Edited=edited)
            folds.Setup()
            if folds.FoldTree()<>folds.FoldArray() || folds.FoldList()<>folds.FoldArray() then failwith "Public fold mismatch"
            let peer = PersistentPeerRead(N=65536,Edited=edited)
            peer.Setup()
            if peer.TreeFold()<>peer.ImmutableListScan() || peer.TreeFold()<>peer.ImmutableListForEach() then failwith "Peer scan mismatch"
        for retain in [false;true] do
            let ints = MixedInt(Retain=retain)
            ints.Setup()
            checkHistories (ints.EditTree()) (ints.EditArray()) (ints.EditList())
            let refs = MixedReference(Retain=retain)
            refs.Setup()
            checkHistories (refs.EditTree()) (refs.EditArray()) (refs.EditList())
            let wide = MixedWide64(Retain=retain)
            wide.Setup()
            checkHistories (wide.EditTree()) (wide.EditArray()) (wide.EditList())
            let peer = PersistentPeerEdits(Retain=retain)
            peer.Setup()
            let trees,others = peer.TreeEdits(),peer.ImmutableListEdits()
            if trees.Length<>others.Length then failwith "Peer root count"
            for i=0 to trees.Length-1 do
                if CacheList.toArray trees[i]<>Array.ofSeq others[i] then failwith "Peer history mismatch"

module Program =
    [<EntryPoint>]
    let main args =
        match args with
        | [|"--probe"|] -> Hardware.probe(); 0
        | [|"--layout"|] -> Layout.run(); 0
        | [|"--retention";payload;collection;n;versions|] ->
            Retention.run payload collection (int n) (int versions)
            0
        | [|"--validate-only"|] ->
            BenchmarkValidation.run()
            printfn "Exact sequence, retained snapshot, payload size, and tree balance checks passed."
            0
        | _ ->
            BenchmarkValidation.run()
            BenchmarkSwitcher.FromAssembly(typeof<TraverseInt>.Assembly).Run(args) |> ignore
            0
