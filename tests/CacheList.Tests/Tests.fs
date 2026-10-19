module CacheList.Tests

open System
open System.Collections.Generic
open System.Threading.Tasks
open CacheCollections
open Xunit

let equal expected actual =
    if expected <> actual then Assert.Fail(sprintf "Expected %A, got %A" expected actual)
let checkTree (source: CacheList<'T>) =
    let rec check isRoot node =
        let n = Tree.arity node
        Assert.InRange(n, (if isRoot then 0 else 16), 32)
        match node with
        | Leaf a -> a.Length, 0
        | Branch (a,s) ->
            if isRoot then Assert.True(a.Length >= 2)
            let results = a |> Array.map (check false)
            equal 1 (results |> Array.map snd |> Array.distinct |> Array.length)
            let mutable total = 0
            results |> Array.iteri (fun i (count,_) -> total <- total + count; equal total s[i])
            total, 1 + snd results[0]
    let count, _ = check true source.Root
    equal source.Length count

[<Theory>]
[<InlineData(0); InlineData(1); InlineData(16); InlineData(32); InlineData(33); InlineData(1024); InlineData(1025); InlineData(32769)>]
let ``bulk build preserves order and occupancy`` n =
    let a = Array.init n id
    let tree = CacheList.ofArray a
    checkTree tree
    equal a (CacheList.toArray tree)
    for i in 0 .. n-1 do equal i tree[i]
    equal n ((tree :> IReadOnlyList<int>).Count)
    equal a [| for x in (tree :> System.Collections.IEnumerable) -> unbox<int> x |]

[<Fact>]
let ``random edits match array model and retain all snapshots`` () =
    for seed in 0 .. 9 do
        let rng = Random(seed)
        let mutable tree = CacheList.init 1200 id
        let mutable model = Array.init 1200 id
        let snapshots = ResizeArray<_>()
        for step in 0 .. 1999 do
            if step % 100 = 0 then snapshots.Add(tree, Array.copy model)
            match rng.Next(3) with
            | 0 ->
                let i = rng.Next(model.Length + 1)
                tree <- CacheList.insertAt i step tree
                model <- Array.insertAt i step model
            | 1 when model.Length > 0 ->
                let i = rng.Next(model.Length)
                tree <- CacheList.removeAt i tree
                model <- Array.removeAt i model
            | _ when model.Length > 0 ->
                let i = rng.Next(model.Length)
                tree <- CacheList.updateAt i (-step) tree
                model[i] <- -step
            | _ -> ()
            checkTree tree
            equal model (CacheList.toArray tree)
        for old, expected in snapshots do equal expected (CacheList.toArray old)

[<Theory>]
[<InlineData(false); InlineData(true)>]
let ``end edits grow and shrink through multiple heights`` front =
    let mutable tree = CacheList.empty
    for i in 0 .. 34999 do
        tree <- if front then CacheList.prepend i tree else CacheList.add i tree
    checkTree tree
    for i in 34999 .. -1 .. 0 do
        equal i (if front then CacheList.head tree else CacheList.last tree)
        tree <- if front then CacheList.tail tree else CacheList.removeLast tree
        if i % 31 = 0 then checkTree tree
    equal true tree.IsEmpty

[<Fact>]
let ``array boundaries and concurrent forks do not mutate shared data`` () =
    let input = Array.init 5000 id
    let tree = CacheList.ofArray input
    input[0] <- -1
    let exported = CacheList.toArray tree
    exported[1] <- -1
    Parallel.For(0, 100, fun i ->
        let fork = tree |> CacheList.updateAt i -1 |> CacheList.add i |> CacheList.removeAt (i+1)
        equal -1 fork[i]
        equal i tree[i]) |> ignore
    equal [|0..4999|] (CacheList.toArray tree)

[<Fact>]
let ``path edits share untouched subtrees`` () =
    let tree = CacheList.init 5000 id
    let edited = CacheList.updateAt 0 -1 tree
    match tree.Root, edited.Root with
    | Branch (a,_), Branch (b,_) -> Assert.True(Object.ReferenceEquals(a[1], b[1]))
    | _ -> failwith "Expected branches"

[<Fact>]
let ``invalid indices and empty operations fail`` () =
    let tree = CacheList.singleton 1
    for i in [-1;1;2] do
        Assert.Throws<ArgumentException>(fun () -> CacheList.item i tree |> ignore) |> ignore
        Assert.Throws<ArgumentException>(fun () -> CacheList.removeAt i tree |> ignore) |> ignore
        Assert.Throws<ArgumentException>(fun () -> CacheList.updateAt i 0 tree |> ignore) |> ignore
    for i in [-1;2] do Assert.Throws<ArgumentException>(fun () -> CacheList.insertAt i 0 tree |> ignore) |> ignore
    equal None (CacheList.tryHead CacheList.empty<int>)
    equal None (CacheList.tryLast CacheList.empty<int>)
    Assert.Throws<ArgumentException>(fun () -> CacheList.tail CacheList.empty<int> |> ignore) |> ignore

[<Fact>]
let ``helpers agree with List`` () =
    let xs = [0..100]
    let tree = CacheList.ofList xs
    equal (List.map ((*) 2) xs) (tree |> CacheList.map ((*) 2) |> CacheList.toList)
    equal (List.filter (fun x -> x % 2 = 0) xs) (tree |> CacheList.filter (fun x -> x % 2 = 0) |> CacheList.toList)
    equal (List.fold (+) 0 xs) (CacheList.fold (+) 0 tree)
    equal (List.scan (+) 0 xs) (tree |> CacheList.scan (+) 0 |> CacheList.toList)
    equal (List.rev xs) (tree |> CacheList.rev |> CacheList.toList)
    equal (List.chunkBySize 17 xs) (tree |> CacheList.chunkBySize 17 |> CacheList.map CacheList.toList |> CacheList.toList)
    equal (List.windowed 3 xs) (tree |> CacheList.windowed 3 |> CacheList.map CacheList.toList |> CacheList.toList)
    equal (List.zip xs xs) (CacheList.zip tree tree |> CacheList.toList)
    equal (List.groupBy (fun x -> x % 3) xs) (tree |> CacheList.groupBy (fun x -> x % 3) |> CacheList.map (fun (k,v) -> k, CacheList.toList v) |> CacheList.toList)
    equal (xs @ xs) (CacheList.append tree tree |> CacheList.toList)
    let a,b = CacheList.splitAt 33 tree
    equal (List.splitAt 33 xs) (CacheList.toList a, CacheList.toList b)
    equal (List.mapFold (fun s x -> x*2,s+x) 0 xs) (let a,s = CacheList.mapFold (fun s x -> x*2,s+x) 0 tree in CacheList.toList a,s)
    Assert.Throws<ArgumentException>(fun () -> CacheList.zip tree (CacheList.singleton 1) |> ignore) |> ignore

[<Fact>]
let ``join handles different heights and repeated concatenation`` () =
    for n in [0;1;15;16;31;32;33;511;1024;1025;33000] do
        for m in [0;1;15;32;33;513;1100;33000] do
            let a,b = CacheList.init n id, CacheList.init m ((+) n)
            let joined = CacheList.append a b
            checkTree joined
            equal [|0..n+m-1|] (CacheList.toArray joined)
            if joined.Length > 0 then
                let removed = CacheList.removeAt (joined.Length/2) joined
                checkTree removed
                equal (Array.removeAt (joined.Length/2) [|0..n+m-1|]) (CacheList.toArray removed)
    let pieces = [0..999] |> List.map CacheList.singleton
    let joined = CacheList.concat pieces
    checkTree joined
    equal [0..999] (CacheList.toList joined)

[<Fact>]
let ``value equality hash and comparison ignore tree shape`` () =
    let a = CacheList.init 1000 id
    let b = Seq.fold (fun t i -> CacheList.add i t) CacheList.empty [0..999]
    Assert.True(a.Equals b)
    equal (a.GetHashCode()) (b.GetHashCode())
    equal 0 (compare a b)
    Assert.True(compare a (CacheList.add 1000 b) < 0)
    Assert.False(a.Equals(CacheList.updateAt 0 -1 b))

[<Fact>]
let ``list adapters preserve ordering and boundary semantics`` () =
    for xs in [[]; [7]; [5;1;3;1;0;4]] do
        let t = CacheList.ofList xs
        let cases = [
            List.choose (fun x -> if x%2=0 then Some(x+1) else None) xs, CacheList.choose (fun x -> if x%2=0 then Some(x+1) else None) t
            List.mapi (+) xs, CacheList.mapi (+) t
            List.sort xs, CacheList.sort t
            List.sortDescending xs, CacheList.sortDescending t
            List.sortBy (~-) xs, CacheList.sortBy (~-) t
            List.sortByDescending (~-) xs, CacheList.sortByDescending (~-) t
            List.sortWith (fun a b -> compare b a) xs, CacheList.sortWith (fun a b -> compare b a) t
            List.distinct xs, CacheList.distinct t
            List.distinctBy (fun x -> x%2) xs, CacheList.distinctBy (fun x -> x%2) t
            List.except [1;4] xs, CacheList.except [1;4] t
            List.takeWhile ((<) 2) xs, CacheList.takeWhile ((<) 2) t
            List.skipWhile ((<) 2) xs, CacheList.skipWhile ((<) 2) t
            List.collect (fun x -> [x;x+1]) xs, CacheList.collect (fun x -> CacheList.ofList [x;x+1]) t
            List.scanBack (+) xs 10, CacheList.scanBack (+) t 10
            List.permute (fun i -> xs.Length-1-i) xs, CacheList.permute (fun i -> xs.Length-1-i) t
            List.insertManyAt 0 [9;8] xs, CacheList.insertManyAt 0 [9;8] t
            List.removeManyAt 0 xs.Length xs, CacheList.removeManyAt 0 xs.Length t
        ]
        for expected,actual in cases do equal expected (CacheList.toList actual)
        for count in 0 .. xs.Length do
            equal (List.take count xs) (CacheList.take count t |> CacheList.toList)
            equal (List.skip count xs) (CacheList.skip count t |> CacheList.toList)
        equal (List.truncate 100 xs) (CacheList.truncate 100 t |> CacheList.toList)
        equal (List.partition ((<) 2) xs) (let a,b = CacheList.partition ((<) 2) t in CacheList.toList a,CacheList.toList b)
        equal (List.pairwise xs) (CacheList.pairwise t |> CacheList.toList)
        equal (List.countBy id xs) (CacheList.countBy id t |> CacheList.toList)
        equal (List.splitInto 3 xs) (CacheList.splitInto 3 t |> CacheList.map CacheList.toList |> CacheList.toList)
        equal (List.indexed xs) (CacheList.indexed t |> CacheList.toList)
        equal (List.tryExactlyOne xs) (CacheList.tryExactlyOne t)
    Assert.Throws<InvalidOperationException>(fun () -> CacheList.take 2 (CacheList.singleton 1) |> ignore) |> ignore
    Assert.Throws<ArgumentException>(fun () -> CacheList.windowed 0 CacheList.empty<int> |> ignore) |> ignore

[<Fact>]
let ``searches numeric reductions and callbacks match List`` () =
    let xs = [5;1;3;1;0;4]
    let t = CacheList.ofList xs
    equal (List.sum xs) (CacheList.sum t)
    equal (List.sumBy ((*) 2) xs) (CacheList.sumBy ((*) 2) t)
    equal (List.average [1.;2.;6.]) (CacheList.average (CacheList.ofList [1.;2.;6.]))
    equal (List.averageBy float xs) (CacheList.averageBy float t)
    equal (List.min xs) (CacheList.min t)
    equal (List.max xs) (CacheList.max t)
    equal (List.minBy (~-) xs) (CacheList.minBy (~-) t)
    equal (List.maxBy (~-) xs) (CacheList.maxBy (~-) t)
    equal (List.reduce (-) xs) (CacheList.reduce (-) t)
    equal (List.reduceBack (-) xs) (CacheList.reduceBack (-) t)
    equal (List.foldBack (-) xs 3) (CacheList.foldBack (-) t 3)
    for p in [(=) 1; (<) 100; (=) 0] do
        equal (List.tryFind p xs) (CacheList.tryFind p t)
        equal (List.tryFindBack p xs) (CacheList.tryFindBack p t)
        equal (List.tryFindIndex p xs) (CacheList.tryFindIndex p t)
        equal (List.tryFindIndexBack p xs) (CacheList.tryFindIndexBack p t)
        equal (List.exists p xs) (CacheList.exists p t)
        equal (List.forall p xs) (CacheList.forall p t)
    equal 1 (CacheList.find ((=) 1) t)
    equal 1 (CacheList.findBack ((=) 1) t)
    equal 1 (CacheList.findIndex ((=) 1) t)
    equal 3 (CacheList.findIndexBack ((=) 1) t)
    equal true (CacheList.contains 3 t)
    let chooser x = if x = 3 then Some "three" else None
    equal (List.pick chooser xs) (CacheList.pick chooser t)
    equal (List.tryPick chooser xs) (CacheList.tryPick chooser t)
    let calls = ResizeArray<_>()
    CacheList.iteri (fun i x -> calls.Add(i,x)) t
    equal (List.indexed xs) (Seq.toList calls)
    calls.Clear()
    CacheList.iter (fun x -> calls.Add(0,x)) t
    equal xs (calls |> Seq.map snd |> Seq.toList)
    equal 42 (CacheList.exactlyOne (CacheList.singleton 42))

[<Fact>]
let ``multiple input operations match List including mismatch errors`` () =
    let xs,ys,zs = [1;2;3], [4;5;6], [7;8;9]
    let a,b,c = CacheList.ofList xs,CacheList.ofList ys,CacheList.ofList zs
    equal (List.map2 (+) xs ys) (CacheList.map2 (+) a b |> CacheList.toList)
    equal (List.mapi2 (fun i x y -> i+x+y) xs ys) (CacheList.mapi2 (fun i x y -> i+x+y) a b |> CacheList.toList)
    equal (List.map3 (fun x y z -> x+y+z) xs ys zs) (CacheList.map3 (fun x y z -> x+y+z) a b c |> CacheList.toList)
    let triples = CacheList.zip3 a b c
    equal (List.zip3 xs ys zs) (CacheList.toList triples)
    equal (xs,ys,zs) (let x,y,z = CacheList.unzip3 triples in CacheList.toList x,CacheList.toList y,CacheList.toList z)
    equal (xs,ys) (let x,y = CacheList.zip a b |> CacheList.unzip in CacheList.toList x,CacheList.toList y)
    equal (List.allPairs xs ys) (CacheList.allPairs a b |> CacheList.toList)
    equal (List.transpose [xs;ys]) (CacheList.transpose [a;b] |> CacheList.map CacheList.toList |> CacheList.toList)
    equal (List.fold2 (fun s x y -> s+x*y) 0 xs ys) (CacheList.fold2 (fun s x y -> s+x*y) 0 a b)
    equal (List.foldBack2 (fun x y s -> x-y-s) xs ys 0) (CacheList.foldBack2 (fun x y s -> x-y-s) a b 0)
    equal (List.mapFoldBack (fun x s -> x+s,s+1) xs 0) (let r,s = CacheList.mapFoldBack (fun x s -> x+s,s+1) a 0 in CacheList.toList r,s)
    equal (List.exists2 (<) xs ys) (CacheList.exists2 (<) a b)
    equal (List.forall2 (<) xs ys) (CacheList.forall2 (<) a b)
    equal (List.compareWith compare xs ys) (CacheList.compareWith compare a b)
    let calls = ResizeArray<_>()
    CacheList.iter2 (fun x y -> calls.Add(x+y)) a b
    equal (List.map2 (+) xs ys) (Seq.toList calls)
    calls.Clear()
    CacheList.iteri2 (fun i x y -> calls.Add(i+x+y)) a b
    equal (List.mapi2 (fun i x y -> i+x+y) xs ys) (Seq.toList calls)
    Assert.Throws<ArgumentException>(fun () -> CacheList.map2 (+) a CacheList.empty<int> |> ignore) |> ignore
    equal [0..4] (CacheList.unfold (fun i -> if i=5 then None else Some(i,i+1)) 0 |> CacheList.toList)

[<Fact>]
let ``enumerators have independent state and support early disposal`` () =
    let tree = CacheList.init 2000 id
    use a = (tree :> seq<int>).GetEnumerator()
    use b = (tree :> seq<int>).GetEnumerator()
    Assert.Throws<InvalidOperationException>(fun () -> a.Current |> ignore) |> ignore
    for i in 0 .. 1999 do
        Assert.True(a.MoveNext())
        equal i a.Current
        if i < 3 then
            Assert.True(b.MoveNext())
            equal i b.Current
    Assert.False(a.MoveNext())
    Assert.False(a.MoveNext())
    Assert.Throws<InvalidOperationException>(fun () -> a.Current |> ignore) |> ignore
    b.Dispose()
    Assert.False(b.MoveNext())
    use empty = (CacheList.empty<int> :> seq<int>).GetEnumerator()
    Assert.False(empty.MoveNext())
    equal [0;1;2] (tree |> Seq.take 3 |> Seq.toList)

[<Fact>]
let ``shared trees support large counts and reject integer overflow`` () =
    // Repeated sharing gives billion-element logical trees without huge allocations.
    let powers = ResizeArray<_>()
    let mutable t = CacheList.singleton 7
    powers.Add t
    for _ in 1 .. 30 do
        t <- CacheList.append t t
        powers.Add t
    equal 1073741824 t.Length
    equal 7 t[t.Length-1]
    Assert.Throws<InvalidOperationException>(fun () -> CacheList.append t t |> ignore) |> ignore
    let maximum = CacheList.concat powers
    equal Int32.MaxValue maximum.Length
    equal 7 maximum[Int32.MaxValue-1]
    equal 8 (CacheList.updateAt (Int32.MaxValue-1) 8 maximum |> CacheList.last)
    equal (Int32.MaxValue-1) (CacheList.removeLast maximum |> CacheList.length)
    Assert.Throws<InvalidOperationException>(fun () -> CacheList.add 8 maximum |> ignore) |> ignore
