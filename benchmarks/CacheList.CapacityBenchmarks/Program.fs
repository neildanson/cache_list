namespace CacheList.CapacityBenchmarks

open System
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

module C16 = CacheCollections.Capacity16.CacheList
module C32 = CacheCollections.Capacity32.CacheList
module C64 = CacheCollections.Capacity64.CacheList

type Inputs() =
    let mutable values = [||]
    let mutable indices = [||]
    let mutable tree16 = C16.empty<int>
    let mutable tree32 = C32.empty<int>
    let mutable tree64 = C64.empty<int>

    [<Params(1024, 65521, 65536, 1000000)>]
    member val N = 0 with get,set

    [<GlobalSetup>]
    member this.Setup() =
        values <- Array.init this.N id
        tree16 <- C16.ofArray values
        tree32 <- C32.ofArray values
        tree64 <- C64.ofArray values
        let rng = Random(1771)
        indices <- Array.init 256 (fun _ -> rng.Next(this.N))

    member _.Values = values
    member _.Indices = indices
    member _.Tree16 = tree16
    member _.Tree32 = tree32
    member _.Tree64 = tree64

[<MemoryDiagnoser>]
type IndexRandom() =
    inherit Inputs()

    [<Benchmark(OperationsPerInvoke = 256)>]
    member this.Width16() =
        let tree = this.Tree16
        let mutable total = 0
        for index in this.Indices do total <- total + tree[index]
        total

    [<Benchmark(Baseline = true, OperationsPerInvoke = 256)>]
    member this.Width32() =
        let tree = this.Tree32
        let mutable total = 0
        for index in this.Indices do total <- total + tree[index]
        total

    [<Benchmark(OperationsPerInvoke = 256)>]
    member this.Width64() =
        let tree = this.Tree64
        let mutable total = 0
        for index in this.Indices do total <- total + tree[index]
        total

[<MemoryDiagnoser>]
type Update() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.updateAt (this.N / 2) -1 this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.updateAt (this.N / 2) -1 this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.updateAt (this.N / 2) -1 this.Tree64

[<MemoryDiagnoser>]
type Append() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.add -1 this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.add -1 this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.add -1 this.Tree64

[<MemoryDiagnoser>]
type Prepend() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.prepend -1 this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.prepend -1 this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.prepend -1 this.Tree64

[<MemoryDiagnoser>]
type InsertMiddle() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.insertAt (this.N / 2) -1 this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.insertAt (this.N / 2) -1 this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.insertAt (this.N / 2) -1 this.Tree64

[<MemoryDiagnoser>]
type RemoveMiddle() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.removeAt (this.N / 2) this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.removeAt (this.N / 2) this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.removeAt (this.N / 2) this.Tree64

[<MemoryDiagnoser>]
type Concat() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.append this.Tree16 this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.append this.Tree32 this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.append this.Tree64 this.Tree64

[<MemoryDiagnoser>]
type Fold() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.fold (fun s x -> s + int64 x) 0L this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.fold (fun s x -> s + int64 x) 0L this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.fold (fun s x -> s + int64 x) 0L this.Tree64

[<MemoryDiagnoser>]
type Map() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.map (fun x -> x + 1) this.Tree16

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.map (fun x -> x + 1) this.Tree32

    [<Benchmark>]
    member this.Width64() = C64.map (fun x -> x + 1) this.Tree64

[<MemoryDiagnoser>]
type Build() =
    inherit Inputs()

    [<Benchmark>]
    member this.Width16() = C16.ofArray this.Values

    [<Benchmark(Baseline = true)>]
    member this.Width32() = C32.ofArray this.Values

    [<Benchmark>]
    member this.Width64() = C64.ofArray this.Values

module Program =
    [<EntryPoint>]
    let main args =
        Checks16.run()
        Checks32.run()
        Checks64.run()
        printfn "All capacity variants passed model, balance, and persistence checks."
        if args <> [|"--validate-only"|] then
            BenchmarkSwitcher.FromAssembly(typeof<Inputs>.Assembly).Run(args) |> ignore
        0
