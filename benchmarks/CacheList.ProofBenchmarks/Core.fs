namespace CacheList.ProofBenchmarks

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Diagnostics
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open CacheCollections

[<Sealed>]
type ReferenceValue(value: int) =
    member val A = int64 value
    member val B = int64 value + 1L
    member val C = int64 value + 2L
    member val D = int64 value + 3L
    member val E = int64 value + 4L
    member val F = int64 value + 5L
    member val G = int64 value + 6L
    member val H = int64 value + 7L

[<Struct>]
type Wide64 =
    val A: int64
    val B: int64
    val C: int64
    val D: int64
    val E: int64
    val F: int64
    val G: int64
    val H: int64
    new(value) =
        let n = int64 value
        { A=n; B=n+1L; C=n+2L; D=n+3L; E=n+4L; F=n+5L; G=n+6L; H=n+7L }

[<Struct>]
type Edit<'T> = { Kind: int; Index: int; Value: 'T }

module Workload =
    let steps = 256
    let prepareEdits make n count =
        let rng = Random(1847)
        let mutable length = n
        Array.init count (fun i ->
            let kind = i % 3
            let index = rng.Next(length + (if kind=1 then 1 else 0))
            length <- length + (if kind=1 then 1 elif kind=2 then -1 else 0)
            { Kind=kind; Index=index; Value=make (-i-1) })
    let treeEdit edit tree =
        match edit.Kind with
        | 0 -> CacheList.updateAt edit.Index edit.Value tree
        | 1 -> CacheList.insertAt edit.Index edit.Value tree
        | _ -> CacheList.removeAt edit.Index tree
    let arrayEdit edit values =
        match edit.Kind with
        | 0 -> Array.updateAt edit.Index edit.Value values
        | 1 -> Array.insertAt edit.Index edit.Value values
        | _ -> Array.removeAt edit.Index values
    let listEdit edit values =
        match edit.Kind with
        | 0 -> List.updateAt edit.Index edit.Value values
        | 1 -> List.insertAt edit.Index edit.Value values
        | _ -> List.removeAt edit.Index values
    let immutableEdit edit (values: ImmutableList<'T>) =
        match edit.Kind with
        | 0 -> values.SetItem(edit.Index,edit.Value)
        | 1 -> values.Insert(edit.Index,edit.Value)
        | _ -> values.RemoveAt edit.Index
    let history apply initial (edits: Edit<'T> array) retain =
        let roots = Array.zeroCreate (if retain then edits.Length+1 else 1)
        let mutable current = initial
        roots[0] <- current
        for i=0 to edits.Length-1 do
            current <- apply edits[i] current
            roots[if retain then i+1 else 0] <- current
        roots
    let input make n =
        // All payloads have the same logical order. Objects are allocated in
        // ordinal order, then shuffled to scatter the referenced payloads.
        let values = Array.init n make
        let rng = Random(777)
        for i=n-1 downto 1 do
            let j = rng.Next(i+1)
            let old = values[i]
            values[i] <- values[j]
            values[j] <- old
        values
    let readInput make n edited =
        let initial = input make n
        let mutable tree = CacheList.ofArray initial
        // Complete update/insert/remove triples preserve the requested length.
        for edit in prepareEdits make n 8190 do tree <- treeEdit edit tree
        let values = CacheList.toArray tree
        // Both shapes contain exactly the same edited values. Only the tree's
        // layout differs; arrays/lists are naturally packed/linked baselines.
        (if edited then tree else CacheList.ofArray values), values, Array.toList values
    let verifyTree (source: CacheList<'T>) =
        let rec check root = function
            | Leaf a ->
                if a.Length>Tree.width || (not root && a.Length<Tree.minimum) then failwith "Leaf occupancy"
                a.Length,0
            | Branch(a,s) ->
                if a.Length>Tree.width || a.Length<(if root then 2 else Tree.minimum) then failwith "Branch occupancy"
                let mutable total = 0
                let mutable depth = -1
                for i=0 to a.Length-1 do
                    let n,d = check false a[i]
                    if depth>=0 && depth<>d then failwith "Unequal depths"
                    depth <- d
                    total <- total+n
                    if s[i]<>total then failwith "Cumulative counts"
                total,depth+1
        let count,_ = check true source.Root
        if count<>source.Length then failwith "Length mismatch"
    let validate make =
        let initial = input make 257
        let edits = prepareEdits make initial.Length 1024
        let mutable tree = CacheList.ofArray initial
        let mutable array = Array.copy initial
        let mutable list = Array.toList initial
        let mutable immutable = ImmutableList.CreateRange initial
        let model = ResizeArray<_>(initial)
        let snapshots = ResizeArray<_>()
        for i=0 to edits.Length-1 do
            let edit = edits[i]
            match edit.Kind with
            | 0 -> model[edit.Index] <- edit.Value
            | 1 -> model.Insert(edit.Index,edit.Value)
            | _ -> model.RemoveAt edit.Index
            tree <- treeEdit edit tree
            array <- arrayEdit edit array
            list <- listEdit edit list
            immutable <- immutableEdit edit immutable
            let expected = model.ToArray()
            if CacheList.toArray tree<>expected || array<>expected || List.toArray list<>expected || Array.ofSeq immutable<>expected then failwith "Model mismatch"
            verifyTree tree
            if i%16=0 then snapshots.Add(tree,array,list,immutable,expected)
        for oldTree,oldArray,oldList,oldImmutable,expected in snapshots do
            if CacheList.toArray oldTree<>expected || oldArray<>expected || List.toArray oldList<>expected || Array.ofSeq oldImmutable<>expected then failwith "Snapshot changed"
        if Marshal.SizeOf<Wide64>()<>64 then failwith "Wrong struct size"
    let validateAll () =
        validate id
        validate ReferenceValue
        validate Wide64

module Hardware =
    let probe () =
        let name = "CacheListProof-" + string Environment.ProcessId
        let mutable success = false
        let mutable reason = ""
        try
            use session = new Microsoft.Diagnostics.Tracing.Session.TraceEventSession(name)
            session.EnableKernelProvider(
                Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords.Process |||
                Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords.Profile,
                Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords.None) |> ignore
            success <- true
            reason <- "Kernel profiling enabled; no hardware-counter run has been recorded yet."
        with error -> reason <- error.GetType().FullName + ": " + error.Message
        JsonSerializer.Serialize {| Session=name; KernelProfilingEnabled=success; Detail=reason |} |> printfn "%s"

module Layout =
    let private describe payload make n edited =
        let tree,_,_ = Workload.readInput make n edited
        Workload.verifyTree tree
        let mutable leaves,branches,entries = 0,0,0
        let mutable smallest,largest = Tree.width,0
        let rec visit = function
            | Leaf values ->
                leaves <- leaves+1
                entries <- entries+values.Length
                smallest <- min smallest values.Length
                largest <- max largest values.Length
                0
            | Branch(children,_) ->
                branches <- branches+1
                let mutable height = 0
                for child in children do height <- max height (visit child)
                height+1
        let height = visit tree.Root
        JsonSerializer.Serialize {|
            Payload=payload; N=n; Edited=edited; BranchLevels=height
            Leaves=leaves; Branches=branches; ValueArrayEntries=entries
            MinimumLeafEntries=smallest; MaximumLeafEntries=largest
            MeanLeafEntries=float entries/float leaves
            TreeStorageObjects=2*leaves+3*branches+1
            ListStorageNodes=n; ArrayStorageObjects=1
        |} |> printfn "%s"
    let run () =
        for n in [65536;1048576] do
            for edited in [false;true] do
                describe "Int" id n edited
                describe "Reference" ReferenceValue n edited
                describe "Wide64" Wide64 n edited
        for edited in [false;true] do describe "Int" id 4194304 edited

module Retention =
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private prime tree array list immutable edits =
        let warm = Array.truncate 9 edits
        Workload.history Workload.treeEdit tree warm false |> GC.KeepAlive
        Workload.history Workload.arrayEdit array warm false |> GC.KeepAlive
        Workload.history Workload.listEdit list warm false |> GC.KeepAlive
        Workload.history Workload.immutableEdit immutable warm false |> GC.KeepAlive
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private measure make (key: 'T -> int64) collection n versions =
        Workload.validate make
        let edited,values,_ = Workload.readInput make n true
        let edits = Workload.prepareEdits make values.Length versions
        let mutable expectedLength = values.Length
        let mutable expectedSum = Array.sumBy key values
        let model = ResizeArray<_>(values)
        let expected = Array.zeroCreate (versions+1)
        expected[0] <- struct(expectedLength,expectedSum)
        for i=0 to versions-1 do
            let e = edits[i]
            match e.Kind with
            | 0 -> expectedSum <- expectedSum - key model[e.Index] + key e.Value; model[e.Index] <- e.Value
            | 1 -> expectedSum <- expectedSum + key e.Value; model.Insert(e.Index,e.Value); expectedLength <- expectedLength+1
            | _ -> expectedSum <- expectedSum - key model[e.Index]; model.RemoveAt e.Index; expectedLength <- expectedLength-1
            expected[i+1] <- struct(expectedLength,expectedSum)
        // Independent model/trace setup is excluded. Keep every base collection
        // alive across both full GCs so the delta measures additional live data.
        let array = Array.copy values
        let list = Array.toList values
        let immutable = ImmutableList.CreateRange values
        prime edited array list immutable edits
        GC.Collect(2,GCCollectionMode.Forced,true,true)
        let baseline = GC.GetTotalMemory(true)
        let allocated = GC.GetAllocatedBytesForCurrentThread()
        let counts = [| for generation in 0..2 -> GC.CollectionCount generation |]
        let pause = GC.GetTotalPauseDuration()
        let clock = Stopwatch.StartNew()
        let kept,validate =
            match collection with
            | "Tree" ->
                let roots = Workload.history Workload.treeEdit edited edits true
                box roots, (fun () ->
                    roots |> Array.iteri (fun i root ->
                        let actual = struct(root.Length,CacheList.sumBy key root)
                        if actual<>expected[i] then failwith "Tree retained snapshot mismatch")
                    Workload.verifyTree roots[roots.Length-1])
            | "Array" ->
                let roots = Workload.history Workload.arrayEdit array edits true
                box roots, (fun () -> roots |> Array.iteri (fun i root ->
                    if struct(root.Length,Array.sumBy key root)<>expected[i] then failwith "Array retained snapshot mismatch"))
            | "List" ->
                let roots = Workload.history Workload.listEdit list edits true
                box roots, (fun () -> roots |> Array.iteri (fun i root ->
                    if struct(root.Length,List.sumBy key root)<>expected[i] then failwith "List retained snapshot mismatch"))
            | "ImmutableList" ->
                let roots = Workload.history Workload.immutableEdit immutable edits true
                box roots, (fun () -> roots |> Array.iteri (fun i root ->
                    if struct(root.Count,Seq.sumBy key root)<>expected[i] then failwith "ImmutableList retained snapshot mismatch"))
            | _ -> invalidArg "collection" collection
        clock.Stop()
        let workAllocated = GC.GetAllocatedBytesForCurrentThread()-allocated
        let workCollections = Array.mapi (fun generation count -> GC.CollectionCount(generation)-count) counts
        let workPause = (GC.GetTotalPauseDuration()-pause).TotalMilliseconds
        let after = GC.GetTotalMemory(true)
        let heap = GC.GetGCMemoryInfo()
        let extraLive = after-baseline
        // Checksums are measured after the live-memory sample, not inside the
        // timed section. Small validation above compares exact whole sequences.
        validate()
        GC.KeepAlive kept
        GC.KeepAlive edited
        GC.KeepAlive array
        GC.KeepAlive list
        GC.KeepAlive immutable
        GC.KeepAlive values
        GC.KeepAlive model
        GC.KeepAlive edits
        GC.KeepAlive expected
        JsonSerializer.Serialize {|
            Collection=collection; N=n; Versions=versions; AdditionalLiveBytes=extraLive
            BaselineLiveBytes=baseline; TotalLiveBytes=after
            AllocatedBytes=workAllocated; WorkMilliseconds=clock.Elapsed.TotalMilliseconds
            WorkGCPauseMilliseconds=workPause; HeapSizeBytes=heap.HeapSizeBytes
            FragmentedBytes=heap.FragmentedBytes; CommittedBytes=heap.TotalCommittedBytes
            Gen0=workCollections[0]; Gen1=workCollections[1]; Gen2=workCollections[2]
            ValidatedSnapshots=versions+1
        |} |> printfn "%s"
    let run payload collection n versions =
        match payload with
        | "Int" -> measure id int64 collection n versions
        | "Reference" -> measure ReferenceValue (fun (x:ReferenceValue) -> x.A) collection n versions
        | "Wide64" -> measure Wide64 (fun (x:Wide64) -> x.A) collection n versions
        | _ -> invalidArg "payload" payload
