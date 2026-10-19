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

