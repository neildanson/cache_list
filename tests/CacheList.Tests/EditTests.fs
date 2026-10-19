module CacheList.EditTests

open System
open CacheCollections
open Xunit
open CacheList.Tests

[<Theory>]
[<InlineData(33); InlineData(1024); InlineData(65536)>]
let ``value updates share immutable counts throughout the changed path`` n =
    let source = CacheList.init n id
    for index in [0;15;16;31;32;n/2;n-1] |> List.distinct do
        let result = CacheList.updateAt index -1 source
        let rec check offset original updated =
            match original,updated with
            | Leaf a,Leaf b ->
                Assert.False(Object.ReferenceEquals(a,b))
                equal (Array.updateAt offset -1 a) b
            | Branch(a,s),Branch(b,t) ->
                Assert.True(Object.ReferenceEquals(s,t))
                Assert.False(Object.ReferenceEquals(a,b))
                let struct (changed,local) = Tree.locate offset s
                for i in 0..a.Length-1 do
                    if i=changed then check local a[i] b[i]
                    else Assert.True(Object.ReferenceEquals(a[i],b[i]))
            | _ -> Assert.Fail("Updating a value changed tree shape")
        check index source.Root result.Root
        checkTree result
        equal -1 result[index]
        equal index source[index]
        // Future length-changing edits must not mutate the shared counts.
        let grown = CacheList.add -2 result
        let shrunk = CacheList.removeAt index result
        checkTree grown
        checkTree shrunk
        checkTree source
        checkTree result
        equal n source.Length
        equal n result.Length

[<Theory>]
[<InlineData(17,32); InlineData(32,32); InlineData(544,1024); InlineData(1024,1024)>]
let ``concatenation reuses valid roots that cannot fit in one node`` n m =
    let left,right = CacheList.init n id,CacheList.init m ((+) n)
    let joined = CacheList.append left right
    checkTree joined
    equal [|0..n+m-1|] (CacheList.toArray joined)
    match joined.Root with
    | Branch(children,_) ->
        equal 2 children.Length
        Assert.True(Object.ReferenceEquals(left.Root,children[0]))
        Assert.True(Object.ReferenceEquals(right.Root,children[1]))
    | _ -> Assert.Fail("Expected a parent joining the two input roots")
    let edited = CacheList.updateAt n -1 joined |> CacheList.removeAt (n-1) |> CacheList.add -2
    checkTree edited
    equal [|0..n-1|] (CacheList.toArray left)
    equal [|n..n+m-1|] (CacheList.toArray right)
    equal [|0..n+m-1|] (CacheList.toArray joined)

[<Theory>]
[<InlineData(1,32); InlineData(15,32); InlineData(32,1); InlineData(33,1024); InlineData(1024,33)>]
let ``underfull roots are redistributed before becoming children`` n m =
    let left,right = CacheList.init n id,CacheList.init m ((+) n)
    let joined = CacheList.append left right
    checkTree joined
    equal [|0..n+m-1|] (CacheList.toArray joined)
    equal [|0..n-1|] (CacheList.toArray left)
    equal [|n..n+m-1|] (CacheList.toArray right)

[<Theory>]
[<InlineData(32); InlineData(1024); InlineData(32768)>]
let ``direct insertion copies handle leaf and branch split boundaries`` n =
    let values = Array.init n string
    let source = CacheList.ofArray values
    let positions =
        if n=32 then [0..32]
        else [0;1;15;16;17;31;32;33;511;512;513;1023;1024;n/2;n-1;n] |> List.distinct
    for index in positions do
        let inserted = CacheList.insertAt index "inserted" source
        checkTree inserted
        equal (Array.insertAt index "inserted" values) (CacheList.toArray inserted)
        // Remove from the new tree as well, retaining both published versions.
        let restored = CacheList.removeAt index inserted
        checkTree restored
        equal values (CacheList.toArray restored)
        equal values (CacheList.toArray source)
        equal "inserted" inserted[index]

let rec private leafLengthAt index = function
    | Leaf values -> values.Length
    | Branch(children,counts) ->
        let struct (child,local) = Tree.locate index counts
        leafLengthAt local children[child]

[<Theory>]
[<InlineData(1024); InlineData(65536)>]
let ``benchmark boundary inputs exercise merging redistribution and spare slots`` n =
    let mutable appendBuilt = CacheList.empty
    for i in 0..n-1 do
        appendBuilt <- CacheList.add i appendBuilt
    let frontFull = appendBuilt |> CacheList.rev |> CacheList.map (fun x -> n-1-x)
    equal 16 (leafLengthAt (n/2) appendBuilt.Root)
    equal 32 (leafLengthAt 0 frontFull.Root)
    equal 16 (leafLengthAt 32 frontFull.Root)
    let merged = CacheList.removeAt (n/2) appendBuilt
    let redistributed = CacheList.removeAt 32 frontFull
    equal 31 (leafLengthAt (n/2) merged.Root)
    equal 23 (leafLengthAt 0 redistributed.Root)
    equal 24 (leafLengthAt 23 redistributed.Root)
    checkTree merged
    checkTree redistributed
    equal (Array.removeAt (n/2) [|0..n-1|]) (CacheList.toArray merged)
    equal (Array.removeAt 32 [|0..n-1|]) (CacheList.toArray redistributed)
    let full = CacheList.init n id
    let spareSlot = CacheList.removeAt 1 full
    equal 31 (leafLengthAt 1 spareSlot.Root)
    let inserted = CacheList.insertAt 1 -1 spareSlot
    checkTree inserted
    equal 32 (leafLengthAt 1 inserted.Root)
    equal (Array.updateAt 1 -1 [|0..n-1|]) (CacheList.toArray inserted)
    for original in [full;appendBuilt;frontFull] do
        checkTree original
        equal [|0..n-1|] (CacheList.toArray original)
