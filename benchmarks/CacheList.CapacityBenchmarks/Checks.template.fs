module CacheList.CapacityBenchmarks.Checks__WIDTH__

open System
open CacheCollections.Capacity__WIDTH__

let private verify (expected: int array) (tree: CacheList<int>) =
    let rec check root node =
        let size = Tree.arity node
        if size > Tree.width || (not root && size < Tree.minimum) then failwith "Invalid occupancy"
        match node with
        | Leaf values -> values.Length,0
        | Branch(children,counts) ->
            if root && children.Length < 2 then failwith "Uncollapsed root"
            let mutable total = 0
            let mutable height = -1
            for i in 0..children.Length-1 do
                let n,h = check false children[i]
                if height >= 0 && height <> h then failwith "Unequal leaf depths"
                height <- h
                total <- total + n
                if counts[i] <> total then failwith "Incorrect cumulative counts"
            total,height+1
    let count,_ = check true tree.Root
    if count <> expected.Length || CacheList.toArray tree <> expected then failwith "Value mismatch"

let run () =
    if Tree.width <> __WIDTH__ || Tree.minimum <> __WIDTH__ / 2 then failwith "Wrong capacity"
    for n in [0;1;Tree.minimum;Tree.width;Tree.width+1;Tree.width*Tree.width+1;65521;65536;1000000] do
        let values = Array.init n id
        let tree = CacheList.ofArray values
        verify values tree
        if n > 0 then
            verify (Array.updateAt (n/2) -1 values) (CacheList.updateAt (n/2) -1 tree)
            verify (Array.removeAt (n/2) values) (CacheList.removeAt (n/2) tree)
        verify (Array.append values [|42|]) (CacheList.add 42 tree)
        verify (Array.insertAt (n/2) 42 values) (CacheList.insertAt (n/2) 42 tree)
        verify (Array.append values values) (CacheList.append tree tree)
        verify (Array.map ((+) 1) values) (CacheList.map ((+) 1) tree)
        verify (Array.filter (fun x -> x%2=0) values) (CacheList.filter (fun x -> x%2=0) tree)
        verify values tree
    let rng = Random(871)
    let mutable values = Array.init (Tree.width*Tree.width+1) id
    let mutable tree = CacheList.ofArray values
    let snapshots = ResizeArray<_>()
    for step in 0..1499 do
        if step%100=0 then snapshots.Add(tree,values)
        match rng.Next(3) with
        | 0 ->
            let i = rng.Next(values.Length+1)
            tree <- CacheList.insertAt i step tree
            values <- Array.insertAt i step values
        | 1 when values.Length>0 ->
            let i = rng.Next(values.Length)
            tree <- CacheList.removeAt i tree
            values <- Array.removeAt i values
        | _ when values.Length>0 ->
            let i = rng.Next(values.Length)
            tree <- CacheList.updateAt i step tree
            values <- Array.updateAt i step values
        | _ -> ()
        if step%20=0 then
            verify values tree
            let start = rng.Next(values.Length+1)
            let count = rng.Next(values.Length-start+1)
            verify (Array.sub values start count) (CacheList.skip start tree |> CacheList.take count)
    verify values tree
    for old,expected in snapshots do verify expected old
