module CacheList.MapTests

open System
open System.Collections
open System.Collections.Generic
open System.Threading.Tasks
open CacheCollections
open Xunit

let equal expected actual = Assert.True(Unchecked.equals expected actual, sprintf "Expected %A, got %A" expected actual)

let checkTree (source: CacheMap<'K,'V>) =
    let cmp = LanguagePrimitives.FastGenericComparer<'K>
    let rec visit isRoot depth node =
        let size = MapTree.arity node
        Assert.InRange(size,(if isRoot then 0 else MapTree.minimum),MapTree.width)
        match node with
        | MapLeaf(k,v) ->
            equal k.Length v.Length
            for i=1 to k.Length-1 do Assert.True(cmp.Compare(k[i-1],k[i])<0)
            k.Length,depth,(if k.Length=0 then None else Some(k[0],k[k.Length-1]))
        | MapBranch(c,bounds,n) ->
            if isRoot then Assert.True(c.Length>=2)
            equal c.Length bounds.Length
            let mutable total = 0
            let mutable lastDepth = -1
            let mutable first = None
            let mutable last = None
            for i=0 to c.Length-1 do
                let count,leafDepth,range = visit false (depth+1) c[i]
                total <- total+count
                if i>0 then equal lastDepth leafDepth
                lastDepth <- leafDepth
                let lo,hi = Option.get range
                equal 0 (cmp.Compare(bounds[i],hi))
                match last with Some previous -> Assert.True(cmp.Compare(previous,lo)<0) | None -> first <- Some lo
                last <- Some hi
            equal total n
            total,lastDepth,Some(Option.get first,Option.get last)
    let n,_,_ = visit true 0 source.Root
    equal source.Count n

let against model actual =
    checkTree actual
    equal (Map.toArray model) (CacheMap.toArray actual)
    equal model.Count actual.Count

[<Theory>]
[<InlineData(0);InlineData(1);InlineData(16);InlineData(32);InlineData(33);InlineData(513);InlineData(1025);InlineData(32769)>]
let ``bulk build and traversal match Map and never alias input`` n =
    let input = Array.init n (fun i -> n-i,i)
    let expected = Map.ofArray input
    let actual = CacheMap.ofArray input
    against expected actual
    if n>0 then input[0] <- -1,-1
    against expected actual
    let exported = CacheMap.toArray actual
    if n>0 then exported[0] <- -2,-2
    against expected actual
    equal (Map.toList expected) (CacheMap.toList actual)
    equal (Map.toSeq expected |> Seq.toArray) (CacheMap.toSeq actual |> Seq.toArray)
    equal (Map.keys expected |> Seq.toArray) (CacheMap.keys actual |> Seq.toArray)
    equal (Map.values expected |> Seq.toArray) (CacheMap.values actual |> Seq.toArray)

[<Theory>]
[<InlineData(11);InlineData(97);InlineData(326)>]
let ``random edits and saved snapshots match independent ordered map`` seed =
    let random = Random seed
    let mutable model = Map.empty
    let mutable actual = CacheMap.empty
    let saved = ResizeArray<_>()
    for step=0 to 7999 do
        let key,value = random.Next(2200),random.Next()
        match random.Next(4) with
        | 0 | 1 -> model <- Map.add key value model; actual <- CacheMap.add key value actual
        | 2 -> model <- Map.remove key model; actual <- CacheMap.remove key actual
        | _ ->
            let updater = function Some x when x%2=0 -> None | _ -> Some value
            model <- Map.change key updater model
            actual <- CacheMap.change key updater actual
        equal (Map.tryFind key model) (CacheMap.tryFind key actual)
        equal (Map.containsKey key model) (CacheMap.containsKey key actual)
        if step%23=0 then against model actual
        if step%211=0 then saved.Add(model,actual)
    against model actual
    for m,a in saved do against m a

[<Theory>]
[<InlineData(false);InlineData(true)>]
let ``growth removal redistribution and root collapse preserve every invariant`` reverse =
    let order = if reverse then [|8191.. -1..0|] else [|0..8191|]
    let mutable source = CacheMap.empty
    for key in order do
        source <- CacheMap.add key key source
        if key%31=0 then checkTree source
    let original = source
    for i=0 to order.Length-1 do
        let key = order[(i*4051)%order.Length]
        source <- CacheMap.remove key source
        equal (8191-i) source.Count
        if i%17=0 then checkTree source
    checkTree source
    Assert.True source.IsEmpty
    against (Map.ofArray(Array.init 8192 (fun i -> i,i))) original

[<Fact>]
let ``module transforms callbacks and short circuiting match Map`` () =
    let random = Random 42
    let input = Array.init 5000 (fun _ -> random.Next(3000),random.Next(100))
    let expected,actual = Map.ofArray input,CacheMap.ofArray input
    let mapped = CacheMap.map (fun k v -> string(k+v)) actual
    against (Map.map (fun k v -> string(k+v)) expected) mapped
    for divisor in [1;2;3;10000] do
        let predicate k v = (k+v)%divisor=0
        against (Map.filter predicate expected) (CacheMap.filter predicate actual)
        let l,r = Map.partition predicate expected
        let a,b = CacheMap.partition predicate actual
        against l a; against r b
    let record apply =
        let order = ResizeArray<_>()
        let result = apply (fun k v -> order.Add k; k%7=0 && v%2=0)
        result,order.ToArray()
    equal (record (fun p -> Map.tryFindKey p expected)) (record (fun p -> CacheMap.tryFindKey p actual))
    equal (Map.fold (fun s k v -> (k,v)::s) [] expected) (CacheMap.fold (fun s k v -> (k,v)::s) [] actual)
    equal (Map.foldBack (fun k v s -> (k,v)::s) expected []) (CacheMap.foldBack (fun k v s -> (k,v)::s) actual [])
    equal (Map.minKeyValue expected) (CacheMap.minKeyValue actual)
    equal (Map.maxKeyValue expected) (CacheMap.maxKeyValue actual)
    equal (Map.exists (fun k _ -> k=17) expected) (CacheMap.exists (fun k _ -> k=17) actual)
    equal (Map.forall (fun k _ -> k>=0) expected) (CacheMap.forall (fun k _ -> k>=0) actual)
    Assert.True(Object.ReferenceEquals(actual,CacheMap.filter (fun _ _ -> true) actual))

[<Fact>]
let ``map shares keys and bounds but replaces every value array`` () =
    let source = CacheMap.ofSeq(seq { for i=0 to 65535 do yield i,i })
    let mapped = CacheMap.map (fun k v -> int64(k+v)) source
    let rec check a b =
        match a,b with
        | MapLeaf(k,v),MapLeaf(l,w) ->
            Assert.True(Object.ReferenceEquals(k,l))
            Assert.False(Object.ReferenceEquals(v,w))
        | MapBranch(c,k,n),MapBranch(d,l,m) ->
            equal n m
            Assert.True(Object.ReferenceEquals(k,l))
            Assert.False(Object.ReferenceEquals(c,d))
            Array.iter2 check c d
        | _ -> Assert.Fail("Map changed shape")
    check source.Root mapped.Root
    checkTree mapped
    equal 65535 source[65535]
    equal 131070L mapped[65535]
    let changed = source.Add(42,-1)
    let rec sharing a b =
        match a,b with
        | MapBranch(c,_,_),MapBranch(d,_,_) ->
            let changed = Array.zip c d |> Array.filter(fun (x,y) -> not(Object.ReferenceEquals(x,y)))
            equal 1 changed.Length
            sharing (fst changed[0]) (snd changed[0])
        | MapLeaf _,MapLeaf _ -> ()
        | _ -> Assert.Fail("Update changed shape")
    sharing source.Root changed.Root
    Assert.True(Object.ReferenceEquals(source,source.Remove -1))

[<CustomEquality;CustomComparison>]
type Representative =
    { Key:int; Label:string }
    override x.Equals other = match other with :? Representative as y -> x.Key=y.Key | _ -> false
    override x.GetHashCode() = x.Key
    interface IComparable with member x.CompareTo other = compare x.Key (unbox<Representative> other).Key

[<Fact>]
let ``equal keys retain last representative including boundary keys`` () =
    let input = Array.init 1000 (fun i -> {Key=i%40;Label=string i},i)
    let expected,actual = Map.ofArray input,CacheMap.ofArray input
    let labels entries = entries |> Array.map(fun (k,v) -> k.Label,v)
    equal (labels(Map.toArray expected)) (labels(CacheMap.toArray actual))
    for i=0 to 39 do
        let key = {Key=i;Label="replaced"}
        equal (Map.add key -1 expected |> Map.toArray |> labels) (CacheMap.add key -1 actual |> CacheMap.toArray |> labels)
        equal (Map.change key (Option.map ((+) 1)) expected |> Map.toArray |> labels)
              (CacheMap.change key (Option.map ((+) 1)) actual |> CacheMap.toArray |> labels)

[<Fact>]
let ``null NaN strings and function values use FSharp key comparison`` () =
    let keys = [|null;"z";"a";null;"A"|] |> Array.mapi(fun i k -> k,i)
    against (Map.ofArray keys) (CacheMap.ofArray keys)
    let floats = [|nan,1;infinity,2;-0.0,3;0.0,4;nan,5;-infinity,6|]
    let expected,actual = Map.ofArray floats,CacheMap.ofArray floats
    checkTree actual
    equal expected.Count actual.Count
    equal (expected |> Map.toArray |> Array.map snd) (actual |> CacheMap.toArray |> Array.map snd)
    equal (Map.tryFind nan expected) (CacheMap.tryFind nan actual)
    let functions = CacheMap.ofArray [|"a",((+) 1);"b",((*) 2)|]
    equal 3 (functions["a"] 2)
    equal 4 ((CacheMap.add "b" ((+) 2) functions)["b"] 2)

[<Fact>]
let ``read only dictionary enumerator and missing keys follow contract`` () =
    let source = CacheMap.ofList [2,"b";1,"a"]
    let view = source :> IReadOnlyDictionary<int,string>
    let mutable result = "unchanged"
    Assert.True(view.TryGetValue(1,&result)); equal "a" result
    Assert.False(view.TryGetValue(3,&result))
    use enumerator = (source :> seq<_>).GetEnumerator()
    Assert.Throws<InvalidOperationException>(fun () -> enumerator.Current |> ignore) |> ignore
    Assert.True(enumerator.MoveNext()); equal (KeyValuePair(1,"a")) enumerator.Current
    Assert.True(enumerator.MoveNext()); equal (KeyValuePair(2,"b")) enumerator.Current
    Assert.False(enumerator.MoveNext()); Assert.False(enumerator.MoveNext())
    Assert.Throws<InvalidOperationException>(fun () -> enumerator.Current |> ignore) |> ignore
    Assert.Throws<NotSupportedException>(fun () -> (enumerator :> IEnumerator).Reset()) |> ignore
    Assert.Throws<KeyNotFoundException>(fun () -> source[3] |> ignore) |> ignore
    Assert.Throws<KeyNotFoundException>(fun () -> CacheMap.minKeyValue CacheMap.empty<int,int> |> ignore) |> ignore
    Assert.Throws<KeyNotFoundException>(fun () -> CacheMap.maxKeyValue CacheMap.empty<int,int> |> ignore) |> ignore
    Assert.Throws<KeyNotFoundException>(fun () -> CacheMap.pick (fun _ _ -> None) source |> ignore) |> ignore
    let mutable calls = 0
    let changed = CacheMap.change 1 (fun old -> calls <- calls+1; equal (Some "a") old; Some "c") source
    equal 1 calls; equal "c" changed[1]; equal "a" source[1]

[<Fact>]
let ``sequence consumed once disposed and callback failure leaves sources intact`` () =
    let mutable visits,disposals = 0,0
    let input = seq { try for i=0 to 90 do visits <- visits+1; yield i,i finally disposals <- disposals+1 }
    let source = CacheMap.ofSeq input
    equal 91 visits; equal 1 disposals
    Assert.Throws<InvalidOperationException>(fun () ->
        CacheMap.map (fun k v -> if k=40 then invalidOp "callback" else v) source |> ignore) |> ignore
    against (Map.ofArray(Array.init 91 (fun i -> i,i))) source
    let broken = seq { try yield 1,1; invalidOp "input" finally disposals <- disposals+1 }
    Assert.Throws<InvalidOperationException>(fun () -> CacheMap.ofSeq broken |> ignore) |> ignore
    equal 2 disposals

[<Fact>]
let ``parallel forks and structural equality are independent of tree shape`` () =
    let source = CacheMap.ofArray(Array.init 4096 (fun i -> i,i))
    let results = Array.init 8 (fun worker -> Task.Run(fun () ->
        let mutable root = source
        for i=0 to 1023 do root <- root.Add(i,worker).Remove(4095-i)
        root))
    Task.WaitAll(results |> Array.map (fun t -> t :> Task))
    for worker=0 to 7 do
        let expected = source |> CacheMap.toArray |> Map.ofArray
        let expected = [0..1023] |> List.fold(fun m i -> m |> Map.add i worker |> Map.remove (4095-i)) expected
        against expected results[worker].Result
    let rebuilt = [4095.. -1..0] |> List.fold(fun m i -> CacheMap.add i i m) CacheMap.empty
    Assert.True(source.Equals rebuilt)
    equal (source.GetHashCode()) (rebuilt.GetHashCode())
    equal 0 ((source :> IComparable).CompareTo rebuilt)
    against (Map.ofArray(Array.init 4096 (fun i -> i,i))) source

[<CustomEquality;CustomComparison>]
type CompareOnly =
    { Order:int; Identity:string }
    override x.Equals other = match other with :? CompareOnly as y -> x.Order=y.Order && x.Identity=y.Identity | _ -> false
    override x.GetHashCode() = hash(x.Order,x.Identity)
    interface IComparable with member x.CompareTo other = compare x.Order (unbox<CompareOnly> other).Order

[<Fact>]
let ``lookup uses comparison while collection equality uses structural equality`` () =
    let a,b = {Order=1;Identity="a"},{Order=1;Identity="b"}
    let left,right = CacheMap.ofList [a,1],CacheMap.ofList [b,1]
    equal (Map.ofList [a,1]).[b] left[b]
    equal ((Map.ofList [a,1]).Equals(Map.ofList [b,1])) (left.Equals right)
    equal 0 ((left :> IComparable).CompareTo right)
    let nanMap = CacheMap.ofList [nan,1]
    equal ((Map.ofList [nan,1]).Equals(Map.ofList [nan,1])) (nanMap.Equals nanMap)

[<Fact>]
let ``all transforming and predicate callbacks follow ordered Map traversal`` () =
    let input = Array.init 500 (fun i -> (i*127)%503,i)
    let expected,actual = Map.ofArray input,CacheMap.ofArray input
    let capture run =
        let visited = ResizeArray<_>()
        run (fun k v -> visited.Add(k,v); k%11=0)
        visited.ToArray()
    equal (capture(fun p -> Map.filter p expected |> ignore)) (capture(fun p -> CacheMap.filter p actual |> ignore))
    equal (capture(fun p -> Map.partition p expected |> ignore)) (capture(fun p -> CacheMap.partition p actual |> ignore))
    equal (capture(fun p -> Map.map p expected |> ignore)) (capture(fun p -> CacheMap.map p actual |> ignore))
    equal (capture(fun p -> Map.exists p expected |> ignore)) (capture(fun p -> CacheMap.exists p actual |> ignore))
    equal (capture(fun p -> Map.forall p expected |> ignore)) (capture(fun p -> CacheMap.forall p actual |> ignore))
    let snapshot = CacheMap.toArray actual
    Assert.Throws<InvalidOperationException>(fun () -> CacheMap.change 10 (fun _ -> invalidOp "updater") actual |> ignore) |> ignore
    Assert.Throws<InvalidOperationException>(fun () -> CacheMap.filter (fun k _ -> if k>20 then invalidOp "predicate" else true) actual |> ignore) |> ignore
    equal snapshot (CacheMap.toArray actual)
    Assert.True(CacheMap.forall (fun _ _ -> false) CacheMap.empty<int,int>)
    Assert.False(CacheMap.exists (fun _ _ -> true) CacheMap.empty<int,int>)
    equal None (CacheMap.tryMinKeyValue CacheMap.empty<int,int>)
    equal None (CacheMap.tryMaxKeyValue CacheMap.empty<int,int>)
