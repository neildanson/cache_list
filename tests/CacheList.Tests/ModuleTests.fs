module CacheList.ModuleTests

open System
open System.Collections.Generic
open CacheCollections
open Xunit
open CacheList.Tests

let private checkedValues expected actual =
    checkTree actual
    equal expected (CacheList.toList actual)

[<Theory>]
[<InlineData(0); InlineData(1); InlineData(31); InlineData(32); InlineData(33); InlineData(1025); InlineData(32769)>]
let ``map preserves shape shares counts and owns transformed values`` n =
    // Editing produces shapes different from a bulk-built tree.
    let source = CacheList.init n id |> CacheList.prepend -1 |> CacheList.removeLast
    let calls = ResizeArray<_>()
    let mapped = CacheList.mapi (fun i x -> calls.Add(i,x); string (i+x)) source
    let xs = CacheList.toList source
    checkedValues (List.mapi (fun i x -> string (i+x)) xs) mapped
    equal (List.indexed xs) (List.ofSeq calls)
    let rec shape original transformed =
        match original, transformed with
        | Leaf a, Leaf b ->
            equal a.Length b.Length
            Assert.False(Object.ReferenceEquals(a,b))
        | Branch(a,s), Branch(b,t) ->
            equal a.Length b.Length
            Assert.True(Object.ReferenceEquals(s,t))
            Assert.False(Object.ReferenceEquals(a,b))
            Array.iter2 shape a b
        | _ -> Assert.Fail("Mapping changed the tree shape")
    shape source.Root mapped.Root
    equal xs (CacheList.toList source)
    checkedValues (List.map ((+) 1) xs) (CacheList.map ((+) 1) source)

[<Theory>]
[<InlineData(0); InlineData(1); InlineData(15); InlineData(16); InlineData(17); InlineData(31); InlineData(32); InlineData(33); InlineData(65); InlineData(1025); InlineData(32769)>]
let ``module transformations retain ordering and balanced leaves`` n =
    let xs = List.init n (fun i -> (i * 17) % 101)
    let t = CacheList.ofList xs
    for predicate in [(fun _ -> false); (fun _ -> true); (fun x -> x % 2 = 0); ((=) 1)] do
        checkedValues (List.filter predicate xs) (CacheList.filter predicate t)
        let a,b = CacheList.partition predicate t
        let x,y = List.partition predicate xs
        checkedValues x a
        checkedValues y b
    let chooser x = if x % 3 = 0 then Some (string x) else None
    let cases = [
        List.rev xs, CacheList.rev t
        List.sort xs, CacheList.sort t
        List.sortDescending xs, CacheList.sortDescending t
        List.distinct xs, CacheList.distinct t
        List.distinctBy (fun x -> x % 7) xs, CacheList.distinctBy (fun x -> x % 7) t
        List.except [1;2;5] xs, CacheList.except [1;2;5] t
        List.collect (fun x -> [x;-x]) xs, CacheList.collect (fun x -> CacheList.ofArray [|x;-x|]) t
    ]
    for expected, actual in cases do checkedValues expected actual
    checkedValues (List.choose chooser xs) (CacheList.choose chooser t)
    checkedValues (List.scan (fun s x -> s + int64 x) 7L xs) (CacheList.scan (fun s x -> s + int64 x) 7L t)
    checkedValues (List.scanBack (fun x s -> int64 x - s) xs 9L) (CacheList.scanBack (fun x s -> int64 x - s) t 9L)
    checkedValues (List.pairwise xs) (CacheList.pairwise t)
    checkedValues (List.countBy (fun x -> x % 7) xs) (CacheList.countBy (fun x -> x % 7) t)
    let groups = CacheList.groupBy (fun x -> x % 7) t
    checkTree groups
    CacheList.iter (snd >> checkTree) groups
    equal (List.groupBy (fun x -> x % 7) xs) (groups |> CacheList.map (fun (k,v) -> k, CacheList.toList v) |> CacheList.toList)
    let forward s x = string (s + x), s - x
    let back x s = string (s + x), x - s
    let a,s = CacheList.mapFold forward 0 t
    checkedValues (fst (List.mapFold forward 0 xs)) a
    equal (snd (List.mapFold forward 0 xs)) s
    let b,s = CacheList.mapFoldBack back t 0
    checkedValues (fst (List.mapFoldBack back xs 0)) b
    equal (snd (List.mapFoldBack back xs 0)) s
    checkedValues xs (CacheList.ofSeq (seq { yield! xs }))
    checkedValues xs (CacheList.init n (fun i -> (i * 17) % 101))

[<Theory>]
[<InlineData(0); InlineData(1); InlineData(33); InlineData(1025); InlineData(32769)>]
let ``slices and bulk edits match List at tree boundaries`` n =
    let xs = [0 .. n-1]
    let t = CacheList.ofList xs
    let cuts = [0;1;15;16;17;31;32;33;n/2;n-1;n] |> List.filter (fun i -> i >= 0 && i <= n) |> List.distinct
    for i in cuts do
        let left,right = CacheList.splitAt i t
        checkedValues (List.take i xs) left
        checkedValues (List.skip i xs) right
        checkedValues (List.take i xs) (CacheList.take i t)
        checkedValues (List.skip i xs) (CacheList.skip i t)
        checkedValues (List.insertManyAt i [-2;-1] xs) (CacheList.insertManyAt i [-2;-1] t)
        for count in [0;1;16;33;n-i] |> List.filter (fun count -> count <= n-i) do
            checkedValues (List.removeManyAt i count xs) (CacheList.removeManyAt i count t)
        checkedValues (List.takeWhile ((>) i) xs) (CacheList.takeWhile ((>) i) t)
        checkedValues (List.skipWhile ((>) i) xs) (CacheList.skipWhile ((>) i) t)
    for size in [1;16;32;33;65] do
        let chunks = CacheList.chunkBySize size t
        checkTree chunks
        CacheList.iter checkTree chunks
        equal (List.chunkBySize size xs) (chunks |> CacheList.map CacheList.toList |> CacheList.toList)
    for count in [1;2;3;32;n+1] do
        let chunks = CacheList.splitInto count t
        checkTree chunks
        CacheList.iter checkTree chunks
        equal (List.splitInto count xs) (chunks |> CacheList.map CacheList.toList |> CacheList.toList)
    Assert.True(Object.ReferenceEquals(t, CacheList.take n t))
    Assert.True(Object.ReferenceEquals(t, CacheList.skip 0 t))
    // Exercise slicing of large logical trees without materialising them.
    let mutable large = CacheList.singleton 7
    for _ in 1 .. 25 do large <- CacheList.append large large
    let tail = CacheList.skip (large.Length - 33) large
    checkedValues (List.replicate 33 7) tail

[<Fact>]
let ``slicing retains fully covered subtrees`` () =
    let t = CacheList.init 65536 id
    let nodes = HashSet<obj>(HashIdentity.Reference)
    let rec collect node =
        nodes.Add(box node) |> ignore
        match node with Leaf _ -> () | Branch(a,_) -> Array.iter collect a
    collect t.Root
    let sliced = CacheList.skip 17 t |> CacheList.take 60000
    checkTree sliced
    let mutable shared = 0
    let rec visit node =
        if nodes.Contains(box node) then shared <- shared + Tree.count node
        else match node with Leaf _ -> () | Branch(a,_) -> Array.iter visit a
    visit sliced.Root
    Assert.True(shared > 59000, sprintf "Only %d elements were shared" shared)

[<Fact>]
let ``sorts are stable and grouping supports structural and null keys`` () =
    let xs = List.init 1000 (fun i -> i % 7, i)
    let t = CacheList.ofList xs
    checkedValues (List.sortBy fst xs) (CacheList.sortBy fst t)
    checkedValues (List.sortByDescending fst xs) (CacheList.sortByDescending fst t)
    let comparer (a,_) (b,_) = compare (b % 3) (a % 3)
    checkedValues (List.sortWith comparer xs) (CacheList.sortWith comparer t)
    let keys = [null; "a"; null; "b"; "a"]
    let groups = CacheList.groupBy id (CacheList.ofList keys)
    equal (List.groupBy id keys) (groups |> CacheList.map (fun (k,v) -> k,CacheList.toList v) |> CacheList.toList)
    checkedValues (List.countBy id keys) (CacheList.countBy id (CacheList.ofList keys))
    checkedValues (List.distinct keys) (CacheList.distinct (CacheList.ofList keys))
    let arrays = [[|1;2|]; [|3|]; [|1;2|]]
    checkedValues (List.distinct arrays) (CacheList.distinct (CacheList.ofList arrays))
    checkedValues (List.countBy id arrays) (CacheList.countBy id (CacheList.ofList arrays))

[<Fact>]
let ``multi input traversals support unequal shapes and preserve short circuiting`` () =
    let xs = [0..1999]
    let a = CacheList.ofList xs
    let b = List.fold (fun tree x -> CacheList.add x tree) CacheList.empty xs
    let c = List.foldBack CacheList.prepend xs CacheList.empty
    checkedValues (List.map2 (+) xs xs) (CacheList.map2 (+) a b)
    checkedValues (List.mapi2 (fun i x y -> i+x-y) xs xs) (CacheList.mapi2 (fun i x y -> i+x-y) a b)
    checkedValues (List.map3 (fun x y z -> x+y-z) xs xs xs) (CacheList.map3 (fun x y z -> x+y-z) a b c)
    checkedValues (List.zip xs xs) (CacheList.zip a b)
    checkedValues (List.zip3 xs xs xs) (CacheList.zip3 a b c)
    let x,y,z = CacheList.zip3 a b c |> CacheList.unzip3
    for t in [x;y;z] do checkedValues xs t
    equal (List.fold2 (fun s x y -> s+x-y) 0 xs xs) (CacheList.fold2 (fun s x y -> s+x-y) 0 a b)
    equal (List.foldBack2 (fun x y s -> x-y-s) xs xs 0) (CacheList.foldBack2 (fun x y s -> x-y-s) a b 0)
    let small = CacheList.singleton 0
    equal true (CacheList.exists2 (=) a small)
    equal false (CacheList.forall2 (<>) a small)
    let calls = ResizeArray<_>()
    Assert.Throws<ArgumentException>(fun () -> CacheList.map2 (fun x y -> calls.Add(x,y); x+y) a small |> ignore) |> ignore
    equal [(0,0)] (List.ofSeq calls)
    calls.Clear()
    Assert.Throws<ArgumentException>(fun () -> CacheList.foldBack2 (fun x y s -> calls.Add(x,y); s) a small 0 |> ignore) |> ignore
    equal [] (List.ofSeq calls)
    let rows = [a;b;c]
    let transposed = CacheList.transpose rows
    checkTree transposed
    CacheList.iter checkTree transposed
    equal (List.transpose [xs;xs;xs]) (transposed |> CacheList.map CacheList.toList |> CacheList.toList)
    Assert.Throws<ArgumentException>(fun () -> CacheList.transpose [a;small] |> ignore) |> ignore

[<Fact>]
let ``callbacks run once in the correct direction and searches stop at matches`` () =
    let xs = [0..1099]
    let t = CacheList.ofList xs
    let calls = ResizeArray<_>()
    let call x = calls.Add x; x
    let forward action =
        calls.Clear()
        action()
        equal xs (List.ofSeq calls)
    forward (fun () -> CacheList.map call t |> ignore)
    forward (fun () -> CacheList.filter (fun x -> call x % 2 = 0) t |> ignore)
    forward (fun () -> CacheList.choose (call >> Some) t |> ignore)
    forward (fun () -> CacheList.partition (fun x -> call x % 2 = 0) t |> ignore)
    forward (fun () -> CacheList.groupBy (fun x -> call x % 3) t |> ignore)
    forward (fun () -> CacheList.countBy (fun x -> call x % 3) t |> ignore)
    for action in [
        (fun () -> CacheList.foldBack (fun x s -> call x + s) t 0 |> ignore)
        (fun () -> CacheList.scanBack (fun x s -> call x + s) t 0 |> ignore)
        (fun () -> CacheList.mapFoldBack (fun x s -> call x,s+1) t 0 |> ignore)
    ] do
        calls.Clear()
        action()
        equal (List.rev xs) (List.ofSeq calls)
    calls.Clear()
    equal (Some 33) (CacheList.tryFind (fun x -> call x = 33) t)
    equal [0..33] (List.ofSeq calls)
    calls.Clear()
    equal (Some 1050) (CacheList.tryFindIndexBack (fun x -> call x = 1050) t)
    equal [1099 .. -1 .. 1050] (List.ofSeq calls)
    calls.Clear()
    CacheList.takeWhile (fun x -> call x < 33) t |> ignore
    equal [0..33] (List.ofSeq calls)
    let failure = InvalidOperationException("callback")
    let mutable count = 0
    let thrown = Assert.Throws<InvalidOperationException>(fun () ->
        CacheList.map (fun x -> count <- count + 1; if x = 33 then raise failure else x) t |> ignore)
    Assert.True(Object.ReferenceEquals(failure, thrown))
    equal 34 count
    checkedValues xs t

let private outcome action =
    try Choice1Of2(action()) with error -> Choice2Of2(error.GetType())

[<Fact>]
let ``boundary arguments and exceptions match List`` () =
    for xs in [[];[1];[1;2;3]] do
        let t = CacheList.ofList xs
        for count in [-2;-1;0;1;2;3;4;Int32.MaxValue] do
            equal (outcome (fun () -> List.take count xs)) (outcome (fun () -> CacheList.take count t |> CacheList.toList))
            equal (outcome (fun () -> List.skip count xs)) (outcome (fun () -> CacheList.skip count t |> CacheList.toList))
            equal (List.truncate count xs) (CacheList.truncate count t |> CacheList.toList)
        for index in [-1;0;1;2;3;4] do
            equal (outcome (fun () -> List.splitAt index xs)) (outcome (fun () -> let a,b = CacheList.splitAt index t in CacheList.toList a,CacheList.toList b))
            for count in [-2;-1;0;1;2;3;4] do
                equal (outcome (fun () -> List.removeManyAt index count xs)) (outcome (fun () -> CacheList.removeManyAt index count t |> CacheList.toList))
        equal (outcome (fun () -> List.exactlyOne xs)) (outcome (fun () -> CacheList.exactlyOne t))
        equal (outcome (fun () -> List.find ((=) 9) xs)) (outcome (fun () -> CacheList.find ((=) 9) t))
        equal (outcome (fun () -> List.reduce (+) xs)) (outcome (fun () -> CacheList.reduce (+) t))
        equal (outcome (fun () -> List.min xs)) (outcome (fun () -> CacheList.min t))
    let overflow = CacheList.ofList [Int32.MaxValue;1]
    Assert.Throws<OverflowException>(fun () -> CacheList.sum overflow |> ignore) |> ignore
    Assert.Throws<OverflowException>(fun () -> CacheList.sumBy id overflow |> ignore) |> ignore
    for xs in [[1.;nan;2.]; [nan;1.;2.]; [infinity;1.;-infinity]; [1.;-0.;0.]] do
        let same a b = Assert.True((Double.IsNaN a && Double.IsNaN b) || a = b)
        let t = CacheList.ofList xs
        same (List.min xs) (CacheList.min t)
        same (List.max xs) (CacheList.max t)
        same (List.minBy id xs) (CacheList.minBy id t)
        same (List.maxBy id xs) (CacheList.maxBy id t)

[<Fact>]
let ``random slices and bulk edits balance trees built by incremental edits`` () =
    let rng = Random(731)
    let mutable xs = [0..1999]
    let mutable t = List.fold (fun tree x -> CacheList.add x tree) CacheList.empty xs
    for step in 0..299 do
        let snapshot = t
        let expectedSnapshot = xs
        let start = rng.Next(xs.Length+1)
        let count = rng.Next(xs.Length-start+1)
        let selected = CacheList.skip start t |> CacheList.take count
        checkedValues (xs |> List.skip start |> List.take count) selected
        if step % 2 = 0 then
            let values = List.init (rng.Next(1,50)) (fun i -> -step-i)
            t <- CacheList.insertManyAt start values t
            xs <- List.insertManyAt start values xs
        else
            let count = Operators.min count 50
            t <- CacheList.removeManyAt start count t
            xs <- List.removeManyAt start count xs
        checkedValues xs t
        checkedValues expectedSnapshot snapshot

[<Fact>]
let ``paired short circuiting and malformed permutations match List callbacks`` () =
    for n in 0..5 do
        for m in 0..5 do
            let xs,ys = List.init n id, List.init m id
            let a,b = CacheList.ofList xs,CacheList.ofList ys
            for predicate in [(fun _ _ -> false); (fun _ _ -> true); (fun x y -> x+y=4)] do
                let expected,actual = ResizeArray<_>(),ResizeArray<_>()
                let left = outcome (fun () -> List.exists2 (fun x y -> expected.Add(x,y); predicate x y) xs ys)
                let right = outcome (fun () -> CacheList.exists2 (fun x y -> actual.Add(x,y); predicate x y) a b)
                equal left right
                equal (List.ofSeq expected) (List.ofSeq actual)
                expected.Clear()
                actual.Clear()
                let left = outcome (fun () -> List.forall2 (fun x y -> expected.Add(x,y); predicate x y) xs ys)
                let right = outcome (fun () -> CacheList.forall2 (fun x y -> actual.Add(x,y); predicate x y) a b)
                equal left right
                equal (List.ofSeq expected) (List.ofSeq actual)
    for mapping in [(fun _ -> 0); (fun i -> if i=1 then -1 else i); (fun i -> if i=1 then 9 else i)] do
        let expected,actual = ResizeArray<_>(),ResizeArray<_>()
        let xs = [10;20;30;40]
        let left = outcome (fun () -> List.permute (fun i -> expected.Add i; mapping i) xs)
        let right = outcome (fun () -> CacheList.permute (fun i -> actual.Add i; mapping i) (CacheList.ofList xs) |> CacheList.toList)
        equal left right
        equal (List.ofSeq expected) (List.ofSeq actual)

[<Fact>]
let ``sequence construction consumes once and disposes on completion and failure`` () =
    let mutable enumerations,disposals = 0,0
    let values = seq {
        enumerations <- enumerations + 1
        try yield! [0..1024]
        finally disposals <- disposals + 1
    }
    checkedValues [0..1024] (CacheList.ofSeq values)
    equal (1,1) (enumerations,disposals)
    let broken = seq {
        try
            yield 1
            failwith "source failure"
        finally disposals <- disposals + 1
    }
    Assert.Throws<Exception>(fun () -> CacheList.ofSeq broken |> ignore) |> ignore
    equal 2 disposals

[<Fact>]
let ``size increasing operations reject overflow before invoking callbacks`` () =
    let powers = ResizeArray<_>()
    let mutable t = CacheList.singleton 7
    powers.Add t
    for _ in 1..30 do
        t <- CacheList.append t t
        powers.Add t
    let maximum = CacheList.concat powers
    let failCallback _ _ = failwith "Should not invoke callback"
    Assert.Throws<InvalidOperationException>(fun () -> CacheList.scan failCallback 0 maximum |> ignore) |> ignore
    Assert.Throws<InvalidOperationException>(fun () -> CacheList.scanBack failCallback maximum 0 |> ignore) |> ignore
    checkedValues [] (CacheList.allPairs maximum CacheList.empty<int>)
    checkedValues [] (CacheList.allPairs CacheList.empty<int> maximum)
