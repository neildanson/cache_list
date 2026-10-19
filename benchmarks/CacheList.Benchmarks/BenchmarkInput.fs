namespace CacheList.Benchmarks

open BenchmarkDotNet.Attributes
open CacheCollections

type BenchmarkInput() =
    let mutable tree = CacheList.empty<int>
    let mutable array = [||]
    let mutable list = []
    [<Params(1024, 65536)>]
    member val N = 0 with get,set
    [<GlobalSetup>]
    member this.Setup() =
        array <- Array.init this.N id
        list <- Array.toList array
        tree <- CacheList.ofArray array

    member _.Tree = tree
    member _.Array = array
    member _.List = list

