namespace CacheList.Benchmarks

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open CacheCollections

[<MemoryDiagnoser>]
type Append() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeAppend() = CacheList.add 42 this.Tree
    [<Benchmark>]
    member this.ArrayAppend() = Array.append this.Array [|42|]
    [<Benchmark>]
    member this.ListAppend() = this.List @ [42]

[<MemoryDiagnoser>]
type Prepend() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreePrepend() = CacheList.prepend 42 this.Tree
    [<Benchmark>]
    member this.ArrayPrepend() = Array.append [|42|] this.Array
    [<Benchmark>]
    member this.ListPrepend() = 42 :: this.List

[<MemoryDiagnoser>]
type RemoveMiddle() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeRemoveMiddle() = CacheList.removeAt (this.N/2) this.Tree
    [<Benchmark>]
    member this.ArrayRemoveMiddle() = Array.removeAt (this.N/2) this.Array
    [<Benchmark>]
    member this.ListRemoveMiddle() = List.removeAt (this.N/2) this.List

[<MemoryDiagnoser>]
type RemoveLast() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeRemoveLast() = CacheList.removeLast this.Tree
    [<Benchmark>]
    member this.ArrayRemoveLast() = Array.take (this.Array.Length-1) this.Array
    [<Benchmark>]
    member this.ListRemoveLast() = List.take (this.List.Length-1) this.List

[<MemoryDiagnoser>]
type Update() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeUpdate() = CacheList.updateAt (this.N/2) 42 this.Tree
    [<Benchmark>]
    member this.ArrayUpdate() = let a = Array.copy this.Array in a[this.N/2] <- 42; a
    [<Benchmark>]
    member this.ListUpdate() = List.updateAt (this.N/2) 42 this.List

[<MemoryDiagnoser>]
type Index() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeIndex() = this.Tree[this.N/2]
    [<Benchmark>]
    member this.ArrayIndex() = this.Array[this.N/2]
    [<Benchmark>]
    member this.ListIndex() = this.List[this.N/2]

[<MemoryDiagnoser>]
type Iterate() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeIterate() = CacheList.fold (fun s x -> s + int64 x) 0L this.Tree
    [<Benchmark>]
    member this.ArrayIterate() = Array.fold (fun s x -> s + int64 x) 0L this.Array
    [<Benchmark>]
    member this.ListIterate() = List.fold (fun s x -> s + int64 x) 0L this.List

[<MemoryDiagnoser>]
type Build() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeBuild() = CacheList.ofArray this.Array
    [<Benchmark>]
    member this.ArrayBuild() = Array.copy this.Array
    [<Benchmark>]
    member this.ListBuild() = Array.toList this.Array

[<MemoryDiagnoser>]
type InsertMiddle() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeInsertMiddle() = CacheList.insertAt (this.N/2) 42 this.Tree
    [<Benchmark>]
    member this.ArrayInsertMiddle() = Array.insertAt (this.N/2) 42 this.Array
    [<Benchmark>]
    member this.ListInsertMiddle() = List.insertAt (this.N/2) 42 this.List

[<MemoryDiagnoser>]
type Concat() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeConcat() = CacheList.append this.Tree this.Tree
    [<Benchmark>]
    member this.ArrayConcat() = Array.append this.Array this.Array
    [<Benchmark>]
    member this.ListConcat() = this.List @ this.List

[<MemoryDiagnoser>]
type Sequence() =
    inherit BenchmarkInput()

    [<Benchmark>]
    member this.TreeSequence() = Seq.fold (fun s x -> s + int64 x) 0L this.Tree
    [<Benchmark>]
    member this.ArraySequence() = Seq.fold (fun s x -> s + int64 x) 0L this.Array
    [<Benchmark>]
    member this.ListSequence() = Seq.fold (fun s x -> s + int64 x) 0L this.List

module Program =
    [<EntryPoint>]
    let main args =
        BenchmarkSwitcher.FromAssembly(typeof<BenchmarkInput>.Assembly).Run(args) |> ignore
        0
