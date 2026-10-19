namespace CacheList.Benchmarks

open BenchmarkDotNet.Attributes
open CacheCollections

// Complement bulk-built inputs with edits that force merges/redistribution,
// insertion without a split, and concatenation with an underfull root.
[<MemoryDiagnoser>]
type EditBoundaries() =
    let mutable appendBuilt = CacheList.empty<int>
    let mutable frontFull = CacheList.empty<int>
    let mutable spareSlot = CacheList.empty<int>
    let mutable full = CacheList.empty<int>
    let singleton = CacheList.singleton -1

    [<Params(1024, 65536)>]
    member val N = 0 with get,set

    [<GlobalSetup>]
    member this.Setup() =
        appendBuilt <- CacheList.empty
        for i = 0 to this.N - 1 do
            appendBuilt <- CacheList.add i appendBuilt
        // Append-built trees end in a full leaf after half-full leaves. Reverse
        // that shape and remap values to retain ascending input order.
        frontFull <- appendBuilt |> CacheList.rev |> CacheList.map (fun x -> this.N - 1 - x)
        full <- CacheList.init this.N id
        spareSlot <- CacheList.removeAt 1 full

    [<Benchmark>]
    member this.TreeRemoveMerge() = CacheList.removeAt (this.N / 2) appendBuilt

    [<Benchmark>]
    member _.TreeRemoveRedistribute() = CacheList.removeAt 32 frontFull

    [<Benchmark>]
    member _.TreeInsertWithoutSplit() = CacheList.insertAt 1 42 spareSlot

    [<Benchmark>]
    member _.TreeConcatUnderfullRoot() = CacheList.append singleton full
