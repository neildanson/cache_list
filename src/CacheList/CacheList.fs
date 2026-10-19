namespace CacheCollections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices

[<assembly: InternalsVisibleTo("CacheList.Tests")>]
do ()

// Arrays are owned by the tree. Every edit allocates before writing.
type internal Node<'T> =
    | Leaf of 'T array
    | Branch of Node<'T> array * int array

// One traversal stack per enumeration, rather than one iterator per tree node.
type internal TreeEnumerator<'T>(root: Node<'T>) =
    let pending = Stack<Node<'T>>()
    let mutable leaf: 'T array = [||]
    let mutable index = -1
    let mutable valid = false
    do pending.Push root
    member private _.Current =
        if not valid then invalidOp "Enumeration has not started or has finished."
        leaf[index]
    interface IEnumerator<'T> with
        member this.Current = this.Current
        member _.Dispose() =
            pending.Clear()
            leaf <- [||]
            index <- -1
            valid <- false
    interface IEnumerator with
        member this.Current = box this.Current
        member _.Reset() = raise (NotSupportedException())
        member _.MoveNext() =
            index <- index + 1
            valid <- index < leaf.Length
            while not valid && pending.Count > 0 do
                match pending.Pop() with
                | Leaf a ->
                    leaf <- a
                    index <- 0
                    valid <- a.Length > 0
                | Branch(a,_) ->
                    for i = a.Length - 1 downto 0 do pending.Push a[i]
            valid

module internal Tree =
    let width = 32
    let minimum = 16
    let count = function Leaf a -> a.Length | Branch (_, s) -> s[s.Length - 1]
    let arity = function Leaf a -> a.Length | Branch (a, _) -> a.Length
    let branch children =
        let mutable total = 0
        let sizes = children |> Array.map (fun c -> total <- total + count c; total)
        Branch(children, sizes)
    let values node =
        { new IEnumerable<'T> with
            member _.GetEnumerator() = new TreeEnumerator<'T>(node) :> IEnumerator<'T>
          interface IEnumerable with
            member _.GetEnumerator() = new TreeEnumerator<'T>(node) :> IEnumerator }
    let locate index (sizes: int array) =
        let mutable lo = 0
        let mutable hi = sizes.Length - 1
        while lo < hi do
            let mid = (lo + hi) / 2
            if index < sizes[mid] then hi <- mid else lo <- mid + 1
        struct (lo, index - (if lo = 0 then 0 else sizes[lo - 1]))
    let rec item index = function
        | Leaf a -> a[index]
        | Branch (a, s) -> let struct (i, j) = locate index s in item j a[i]
    let replace i value (a: 'T array) =
        let result = Array.copy a
        result[i] <- value
        result
    let rec update index value = function
        | Leaf a -> Leaf(replace index value a)
        | Branch (a, s) ->
            let struct (i, j) = locate index s
            branch (replace i (update j value a[i]) a)
    let splice i remove (insert: 'T array) (a: 'T array) =
        let result = Array.zeroCreate (a.Length - remove + insert.Length)
        Array.Copy(a, 0, result, 0, i)
        Array.Copy(insert, 0, result, i, insert.Length)
        Array.Copy(a, i + remove, result, i + insert.Length, a.Length - i - remove)
        result
    let splitLeaf a =
        if Array.length a <= width then [| Leaf a |]
        else [| Leaf a[..minimum-1]; Leaf a[minimum..] |]
    let splitBranch a =
        if Array.length a <= width then [| branch a |]
        else [| branch a[..minimum-1]; branch a[minimum..] |]
    let rec insert index value = function
        | Leaf a -> splice index 0 [|value|] a |> splitLeaf
        | Branch (a, s) ->
            let struct (i, j) = if index = s[s.Length-1] then struct (a.Length - 1, count a[a.Length - 1]) else locate index s
            splice i 1 (insert j value a[i]) a |> splitBranch
    let combine left right =
        match left, right with
        | Leaf a, Leaf b ->
            let all = Array.append a b
            if all.Length <= width then [| Leaf all |]
            else let half = all.Length / 2 in [| Leaf all[..half-1]; Leaf all[half..] |]
        | Branch (a,_), Branch (b,_) ->
            let all = Array.append a b
            if all.Length <= width then [| branch all |]
            else let half = all.Length / 2 in [| branch all[..half-1]; branch all[half..] |]
        | _ -> invalidOp "Unbalanced tree"
    let rec remove index = function
        | Leaf a -> Leaf(splice index 1 [||] a)
        | Branch (a, s) ->
            let struct (i, j) = locate index s
            let children = replace i (remove j a[i]) a
            if arity children[i] < minimum && children.Length > 1 then
                let first = if i = 0 then 0 else i - 1
                branch (splice first 2 (combine children[first] children[first+1]) children)
            else branch children
    let rec normalize = function
        | Branch (a, _) when a.Length = 1 -> normalize a[0]
        | node -> node
    let rec height = function Leaf _ -> 0 | Branch(a,_) -> 1 + height a[0]
    let rec join left lh right rh =
        if lh = rh then combine left right
        elif lh > rh then
            match left with
            | Branch(a,_) -> splice (a.Length-1) 1 (join a[a.Length-1] (lh-1) right rh) a |> splitBranch
            | _ -> invalidOp "Invalid height"
        else
            match right with
            | Branch(a,_) -> splice 0 1 (join left lh a[0] (rh-1)) a |> splitBranch
            | _ -> invalidOp "Invalid height"
    let rec fold folder state = function
        | Leaf a -> Array.fold folder state a
        | Branch(a,_) -> Array.fold (fold folder) state a
    // Linear-time bulk build, distributing each level evenly to maintain occupancy.
    let groups (a: 'T array) =
        let n = if a.Length = 0 then 0 else 1 + (a.Length - 1) / width
        if n = 0 then [||] else
        let small, extra = a.Length / n, a.Length % n
        Array.init n (fun i -> let start = i * small + min i extra in a[start .. start + small + (if i < extra then 1 else 0) - 1])
    let ofArray a =
        let rec build nodes =
            if Array.length nodes = 1 then nodes[0]
            else nodes |> groups |> Array.map branch |> build
        if Array.isEmpty a then Leaf [||]
        else a |> groups |> Array.map Leaf |> build

/// A persistent indexed sequence with private, packed leaf arrays.
[<Sealed>]
type CacheList<'T> internal (root: Node<'T>) =
    member internal _.Root = root
    member _.Length = Tree.count root
    member _.Count = Tree.count root
    member _.IsEmpty = Tree.count root = 0
    member _.Item
        with get index =
            if index < 0 || index >= Tree.count root then invalidArg "index" "Index outside collection."
            Tree.item index root
    override this.Equals(other) =
        match other with
        | :? CacheList<'T> as that ->
            this.Length = that.Length && Seq.forall2 Unchecked.equals (Tree.values root) (Tree.values that.Root)
        | _ -> false
    override _.GetHashCode() = Tree.fold (fun hash x -> (hash * 31) + Unchecked.hash x) 17 root
    interface IComparable with
        member _.CompareTo(other) =
            match other with
            | null -> 1
            | :? CacheList<'T> as that -> Seq.compareWith Unchecked.compare (Tree.values root) (Tree.values that.Root)
            | _ -> invalidArg "other" "Collections must have the same element type."
    interface IReadOnlyList<'T> with
        member this.Item with get index = this[index]
        member this.Count = this.Count
    interface IEnumerable<'T> with
        member _.GetEnumerator() = (Tree.values root).GetEnumerator()
    interface IEnumerable with
        member _.GetEnumerator() = (Tree.values root :> IEnumerable).GetEnumerator()

[<RequireQualifiedAccess>]
module CacheList =
    let empty<'T> = CacheList<'T>(Leaf [||])
    let isEmpty (source: CacheList<'T>) = source.IsEmpty
    let length (source: CacheList<'T>) = source.Length
    let toSeq (source: CacheList<'T>) = source :> seq<'T>
    let ofArray source = CacheList(Tree.ofArray source)
    let ofSeq source = source |> Seq.toArray |> ofArray
    let ofList source = source |> List.toArray |> ofArray
    let toArray source = source |> toSeq |> Seq.toArray
    let toList source = source |> toSeq |> Seq.toList
    let singleton value = CacheList(Leaf [|value|])
    let init count initializer = Array.init count initializer |> ofArray
    let replicate count value = init count (fun _ -> value)
    let item index (source: CacheList<'T>) = source[index]
    let tryItem index (source: CacheList<'T>) = if index < 0 || index >= source.Length then None else Some source[index]
    let insertAt index value (source: CacheList<'T>) =
        if index < 0 || index > source.Length then invalidArg "index" "Index outside collection."
        if source.Length = Int32.MaxValue then invalidOp "Collection is too large."
        let nodes = Tree.insert index value source.Root
        CacheList(if nodes.Length = 1 then nodes[0] else Tree.branch nodes)
    let add value source = insertAt (length source) value source
    let prepend value source = insertAt 0 value source
    let cons value source = prepend value source
    let updateAt index value (source: CacheList<'T>) =
        if index < 0 || index >= source.Length then invalidArg "index" "Index outside collection."
        CacheList(Tree.update index value source.Root)
    let removeAt index (source: CacheList<'T>) =
        if index < 0 || index >= source.Length then invalidArg "index" "Index outside collection."
        CacheList(Tree.remove index source.Root |> Tree.normalize)
    let head source = item 0 source
    let last source = item (length source - 1) source
    let tryHead source = tryItem 0 source
    let tryLast source = tryItem (length source - 1) source
    let tail source = removeAt 0 source
    let removeLast source = removeAt (length source - 1) source
    let append (left: CacheList<'T>) (right: CacheList<'T>) =
        if left.IsEmpty then right
        elif right.IsEmpty then left
        else
            if int64 left.Length + int64 right.Length > int64 Int32.MaxValue then invalidOp "Collection is too large."
            let nodes = Tree.join left.Root (Tree.height left.Root) right.Root (Tree.height right.Root)
            CacheList(if nodes.Length = 1 then nodes[0] else Tree.branch nodes)
    let concat sources = Seq.fold append empty sources
    let collect mapping source = source |> toSeq |> Seq.collect (mapping >> toSeq) |> ofSeq
    let map mapping source = source |> toSeq |> Seq.map mapping |> ofSeq
    let mapi mapping source = source |> toSeq |> Seq.mapi mapping |> ofSeq
    let filter predicate source = source |> toSeq |> Seq.filter predicate |> ofSeq
    let choose chooser source = source |> toSeq |> Seq.choose chooser |> ofSeq
    let rev source = source |> toArray |> Array.rev |> ofArray
    let fold folder state (source: CacheList<'T>) = Tree.fold folder state source.Root
    let iter action source = fold (fun () value -> action value) () source
    let iteri action source = Seq.iteri action (toSeq source)
    let exists predicate source = Seq.exists predicate (toSeq source)
    let forall predicate source = Seq.forall predicate (toSeq source)
    let contains value source = Seq.contains value (toSeq source)
    let find predicate source = Seq.find predicate (toSeq source)
    let tryFind predicate source = Seq.tryFind predicate (toSeq source)
    let findIndex predicate source = Seq.findIndex predicate (toSeq source)
    let tryFindIndex predicate source = Seq.tryFindIndex predicate (toSeq source)
    let pick chooser source = Seq.pick chooser (toSeq source)
    let tryPick chooser source = Seq.tryPick chooser (toSeq source)
    let inline sum source = Seq.sum (toSeq source)
    let inline sumBy projection source = Seq.sumBy projection (toSeq source)
    let inline average source = Seq.average (toSeq source)
    let inline averageBy projection source = Seq.averageBy projection (toSeq source)
    let min source = Seq.min (toSeq source)
    let max source = Seq.max (toSeq source)
    let minBy projection source = Seq.minBy projection (toSeq source)
    let maxBy projection source = Seq.maxBy projection (toSeq source)
    // Less performance-sensitive operations delegate to List to retain its semantics.
    let take count source = source |> toList |> List.take count |> ofList
    let skip count source = source |> toList |> List.skip count |> ofList
    let truncate count source = source |> toList |> List.truncate count |> ofList
    let takeWhile predicate source = source |> toList |> List.takeWhile predicate |> ofList
    let skipWhile predicate source = source |> toList |> List.skipWhile predicate |> ofList
    let splitAt index source =
        let a, b = List.splitAt index (toList source)
        ofList a, ofList b
    let partition predicate source =
        let a, b = List.partition predicate (toList source)
        ofList a, ofList b
    let sort source = source |> toList |> List.sort |> ofList
    let sortBy projection source = source |> toList |> List.sortBy projection |> ofList
    let sortWith comparer source = source |> toList |> List.sortWith comparer |> ofList
    let sortDescending source = source |> toList |> List.sortDescending |> ofList
    let sortByDescending projection source = source |> toList |> List.sortByDescending projection |> ofList
    let distinct source = source |> toList |> List.distinct |> ofList
    let distinctBy projection source = source |> toList |> List.distinctBy projection |> ofList
    let except exclusions source = source |> toList |> List.except exclusions |> ofList
    let indexed source = mapi (fun i x -> i, x) source
    let pairwise source = source |> toList |> List.pairwise |> ofList
    let windowed size source = source |> toList |> List.windowed size |> List.map ofList |> ofList
    let chunkBySize size source = source |> toList |> List.chunkBySize size |> List.map ofList |> ofList
    let splitInto count source = source |> toList |> List.splitInto count |> List.map ofList |> ofList
    let groupBy projection source = source |> toList |> List.groupBy projection |> List.map (fun (k,v) -> k, ofList v) |> ofList
    let countBy projection source = source |> toList |> List.countBy projection |> ofList
    let zip left right = List.zip (toList left) (toList right) |> ofList
    let zip3 a b c = List.zip3 (toList a) (toList b) (toList c) |> ofList
    let unzip source =
        let a,b = List.unzip (toList source)
        ofList a, ofList b
    let unzip3 source =
        let a,b,c = List.unzip3 (toList source)
        ofList a, ofList b, ofList c
    let map2 mapping a b = List.map2 mapping (toList a) (toList b) |> ofList
    let mapi2 mapping a b = List.mapi2 mapping (toList a) (toList b) |> ofList
    let map3 mapping a b c = List.map3 mapping (toList a) (toList b) (toList c) |> ofList
    let iter2 action a b = List.iter2 action (toList a) (toList b)
    let iteri2 action a b = List.iteri2 action (toList a) (toList b)
    let exists2 predicate a b = List.exists2 predicate (toList a) (toList b)
    let forall2 predicate a b = List.forall2 predicate (toList a) (toList b)
    let fold2 folder state a b = List.fold2 folder state (toList a) (toList b)
    let foldBack folder source state = List.foldBack folder (toList source) state
    let foldBack2 folder a b state = List.foldBack2 folder (toList a) (toList b) state
    let reduce reduction source = List.reduce reduction (toList source)
    let reduceBack reduction source = List.reduceBack reduction (toList source)
    let scan folder state source = List.scan folder state (toList source) |> ofList
    let scanBack folder source state = List.scanBack folder (toList source) state |> ofList
    let mapFold mapping state source =
        let a,s = List.mapFold mapping state (toList source)
        ofList a,s
    let mapFoldBack mapping source state =
        let a,s = List.mapFoldBack mapping (toList source) state
        ofList a,s
    let allPairs a b = List.allPairs (toList a) (toList b) |> ofList
    let permute mapping source = List.permute mapping (toList source) |> ofList
    let unfold generator state = List.unfold generator state |> ofList
    let compareWith comparer a b = List.compareWith comparer (toList a) (toList b)
    let findBack predicate source = List.findBack predicate (toList source)
    let tryFindBack predicate source = List.tryFindBack predicate (toList source)
    let findIndexBack predicate source = List.findIndexBack predicate (toList source)
    let tryFindIndexBack predicate source = List.tryFindIndexBack predicate (toList source)
    let tryExactlyOne source = List.tryExactlyOne (toList source)
    let exactlyOne source = List.exactlyOne (toList source)
    let insertManyAt index values source = List.insertManyAt index values (toList source) |> ofList
    let removeManyAt index count source = List.removeManyAt index count (toList source) |> ofList
    let transpose sources = sources |> Seq.map toList |> Seq.toList |> List.transpose |> List.map ofList |> ofList
