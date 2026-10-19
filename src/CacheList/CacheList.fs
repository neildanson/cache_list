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
type internal TreeEnumerator<'T>(root: Node<'T>, ?reverse: bool) =
    let backwards = defaultArg reverse false
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
            index <- index + (if backwards then -1 else 1)
            valid <- index >= 0 && index < leaf.Length
            while not valid && pending.Count > 0 do
                match pending.Pop() with
                | Leaf a ->
                    leaf <- a
                    index <- if backwards then a.Length - 1 else 0
                    valid <- a.Length > 0
                | Branch(a,_) ->
                    if backwards then
                        for i = 0 to a.Length - 1 do pending.Push a[i]
                    else
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
        | Branch(a,_) ->
            let mutable result = state
            for child in a do result <- fold folder result child
            result
    let rec foldBack folder node state =
        match node with
        | Leaf a -> Array.foldBack folder a state
        | Branch(a,_) ->
            let mutable result = state
            for i = a.Length - 1 downto 0 do result <- foldBack folder a[i] result
            result
    let rec iter action = function
        | Leaf a -> Array.iter action a
        | Branch(a,_) -> for child in a do iter action child
    // Length-preserving transforms reuse immutable cumulative counts, but own
    // every new child/value array. Callbacks run in logical element order.
    let rec map mapping = function
        | Leaf a -> Leaf(Array.map mapping a)
        | Branch(a,s) -> Branch(Array.map (map mapping) a, s)
    let rec mapBack mapping = function
        | Leaf a ->
            let result = Array.zeroCreate a.Length
            for i = a.Length - 1 downto 0 do result[i] <- mapping a[i]
            Leaf result
        | Branch(a,s) ->
            let result = Array.zeroCreate a.Length
            for i = a.Length - 1 downto 0 do result[i] <- mapBack mapping a[i]
            Branch(result,s)
    let rec unzip = function
        | Leaf a ->
            let left,right = Array.unzip a
            Leaf left, Leaf right
        | Branch(a,s) ->
            let left,right = Array.zeroCreate a.Length, Array.zeroCreate a.Length
            for i = 0 to a.Length - 1 do
                let x,y = unzip a[i]
                left[i] <- x
                right[i] <- y
            Branch(left,s), Branch(right,s)
    let rec unzip3 = function
        | Leaf a ->
            let x,y,z = Array.unzip3 a
            Leaf x, Leaf y, Leaf z
        | Branch(a,s) ->
            let x,y,z = Array.zeroCreate a.Length, Array.zeroCreate a.Length, Array.zeroCreate a.Length
            for i = 0 to a.Length - 1 do
                let a,b,c = unzip3 a[i]
                x[i] <- a
                y[i] <- b
                z[i] <- c
            Branch(x,s), Branch(y,s), Branch(z,s)
    let rec rev = function
        | Leaf a -> Leaf(Array.rev a)
        | Branch(a,_) -> branch (Array.init a.Length (fun i -> rev a[a.Length-1-i]))
    let rec tryPick chooser = function
        | Leaf a -> Array.tryPick chooser a
        | Branch(a,_) -> Array.tryPick (tryPick chooser) a
    let rec tryPickBack chooser = function
        | Leaf a ->
            let mutable i = a.Length - 1
            let mutable result = None
            while i >= 0 && Option.isNone result do
                result <- chooser a[i]
                i <- i - 1
            result
        | Branch(a,_) ->
            let mutable i = a.Length - 1
            let mutable result = None
            while i >= 0 && Option.isNone result do
                result <- tryPickBack chooser a[i]
                i <- i - 1
            result
    let toArray node =
        let result = Array.zeroCreate (count node)
        let mutable offset = 0
        let rec copy = function
            | Leaf a ->
                Array.Copy(a, 0, result, offset, a.Length)
                offset <- offset + a.Length
            | Branch(a,_) -> Array.iter copy a
        copy node
        result
    let append left right =
        if count left = 0 then right
        elif count right = 0 then left
        else
            if int64 (count left) + int64 (count right) > int64 Int32.MaxValue then invalidOp "Collection is too large."
            let nodes = join left (height left) right (height right)
            if nodes.Length = 1 then nodes[0] else branch nodes
    // Fully covered subtrees are returned unchanged; only boundaries are copied.
    // Joining partial roots restores occupancy and equal leaf depth.
    let rec slice start length node =
        if length = 0 then Leaf [||]
        elif start = 0 && length = count node then node
        else
            match node with
            | Leaf a -> Leaf(Array.sub a start length)
            | Branch(a,s) ->
                let struct (first, offset) = locate start s
                let struct (last, lastOffset) = locate (start + length - 1) s
                if first = last then slice offset length a[first]
                else
                    let partialLeft = offset > 0
                    let partialRight = lastOffset + 1 < count a[last]
                    let middleStart = first + (if partialLeft then 1 else 0)
                    let middleEnd = last - (if partialRight then 1 else 0)
                    let middle =
                        if middleStart > middleEnd then Leaf [||]
                        elif middleStart = middleEnd then a[middleStart]
                        else branch (Array.sub a middleStart (middleEnd - middleStart + 1))
                    let left =
                        if partialLeft then append (slice offset (count a[first] - offset) a[first]) middle
                        else middle
                    if partialRight then append left (slice 0 (lastOffset + 1) a[last])
                    else left
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
    // Stable merges retain List's order for equal keys/comparer results.
    let stableSortInPlaceWith comparer (values: 'T array) =
        let buffer = Array.zeroCreate values.Length
        let rec sort start length =
            if length > 1 then
                let leftLength = length / 2
                let middle = start + leftLength
                let finish = start + length
                sort start leftLength
                sort middle (length - leftLength)
                let mutable left = start
                let mutable right = middle
                for target = start to finish - 1 do
                    if left < middle && (right = finish || comparer values[left] values[right] <= 0) then
                        buffer[target] <- values[left]
                        left <- left + 1
                    else
                        buffer[target] <- values[right]
                        right <- right + 1
                Array.Copy(buffer, start, values, start, length)
        sort 0 values.Length

// Streaming construction retains only packed leaves, rather than a full-size
// staging array or a linked list. The final two leaves are balanced on finish.
type internal TreeBuilder<'T>() =
    let leaves = ResizeArray<Node<'T>>()
    let mutable buffer = Array.zeroCreate<'T> Tree.width
    let mutable used = 0
    let mutable count = 0
    member _.Add(value) =
        if count = Int32.MaxValue then invalidOp "Collection is too large."
        if used = Tree.width then
            leaves.Add(Leaf buffer)
            buffer <- Array.zeroCreate Tree.width
            used <- 0
        buffer[used] <- value
        used <- used + 1
        count <- count + 1
    member _.Finish() =
        if used > 0 then
            let last = Leaf(if used = Tree.width then buffer else Array.sub buffer 0 used)
            if used < Tree.minimum && leaves.Count > 0 then
                let i = leaves.Count - 1
                let balanced = Tree.combine leaves[i] last
                leaves.RemoveAt i
                leaves.AddRange balanced
            else leaves.Add last
        let rec build nodes =
            if Array.length nodes = 1 then nodes[0]
            else nodes |> Tree.groups |> Array.map Tree.branch |> build
        if count = 0 then Leaf [||] else build (leaves.ToArray())

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
        member _.GetEnumerator() = new TreeEnumerator<'T>(root) :> IEnumerator<'T>
    interface IEnumerable with
        member _.GetEnumerator() = new TreeEnumerator<'T>(root) :> IEnumerator

[<RequireQualifiedAccess>]
module CacheList =
    let empty<'T> = CacheList<'T>(Leaf [||])
    let isEmpty (source: CacheList<'T>) = source.IsEmpty
    let length (source: CacheList<'T>) = source.Length
    let toSeq (source: CacheList<'T>) = source :> seq<'T>
    let ofArray source = CacheList(Tree.ofArray source)
    let ofSeq (source: seq<'T>) =
        if isNull source then nullArg "source"
        let builder = TreeBuilder<'T>()
        for value in source do builder.Add value
        CacheList(builder.Finish())
    let ofList source =
        let builder = TreeBuilder<_>()
        List.iter builder.Add source
        CacheList(builder.Finish())
    let toArray (source: CacheList<'T>) = Tree.toArray source.Root
    let toList (source: CacheList<'T>) = Tree.foldBack (fun x xs -> x :: xs) source.Root []
    let singleton value = CacheList(Leaf [|value|])
    let init count initializer =
        if count < 0 then invalidArg "count" "The input must be non-negative."
        let builder = TreeBuilder<_>()
        for i = 0 to count - 1 do builder.Add(initializer i)
        CacheList(builder.Finish())
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
    let concat (sources: seq<CacheList<'T>>) =
        if isNull sources then nullArg "sources"
        let mutable result = empty
        for source in sources do result <- append result source
        result
    let map mapping (source: CacheList<'T>) = CacheList(Tree.map mapping source.Root)
    let mapi mapping source =
        let mutable index = -1
        map (fun value -> index <- index + 1; mapping index value) source
    let fold folder state (source: CacheList<'T>) = Tree.fold folder state source.Root
    let foldBack folder (source: CacheList<'T>) state = Tree.foldBack folder source.Root state
    let iter action (source: CacheList<'T>) = Tree.iter action source.Root
    let iteri action source =
        let mutable index = -1
        iter (fun value -> index <- index + 1; action index value) source
    let collect mapping source =
        let builder = TreeBuilder<_>()
        iter (fun value -> iter builder.Add (mapping value)) source
        CacheList(builder.Finish())
    let filter predicate source =
        let builder = TreeBuilder<_>()
        iter (fun value -> if predicate value then builder.Add value) source
        CacheList(builder.Finish())
    let choose chooser source =
        let builder = TreeBuilder<_>()
        iter (fun value -> match chooser value with Some x -> builder.Add x | None -> ()) source
        CacheList(builder.Finish())
    let rev (source: CacheList<'T>) = CacheList(Tree.rev source.Root)
    let tryPick chooser (source: CacheList<'T>) = Tree.tryPick chooser source.Root
    let pick chooser source =
        match tryPick chooser source with
        | Some value -> value
        | None -> raise (KeyNotFoundException())
    let tryFind predicate source = tryPick (fun x -> if predicate x then Some x else None) source
    let find predicate source = pick (fun x -> if predicate x then Some x else None) source
    let exists predicate source = tryPick (fun x -> if predicate x then Some () else None) source |> Option.isSome
    let forall predicate source = not (exists (predicate >> not) source)
    let contains value source = exists (Unchecked.equals value) source
    let tryFindIndex predicate source =
        let mutable index = -1
        tryPick (fun x -> index <- index + 1; if predicate x then Some index else None) source
    let findIndex predicate source =
        match tryFindIndex predicate source with
        | Some index -> index
        | None -> raise (KeyNotFoundException())
    let inline sum source = fold (fun state x -> Checked.(+) state x) LanguagePrimitives.GenericZero source
    let inline sumBy projection source = fold (fun state x -> Checked.(+) state (projection x)) LanguagePrimitives.GenericZero source
    let inline average source =
        if isEmpty source then invalidArg "source" "The input sequence was empty."
        LanguagePrimitives.DivideByInt (sum source) (length source)
    let inline averageBy projection source =
        if isEmpty source then invalidArg "source" "The input sequence was empty."
        LanguagePrimitives.DivideByInt (sumBy projection source) (length source)
    let reduce reduction source =
        if isEmpty source then invalidArg "source" "The input list was empty."
        let mutable first = true
        fold (fun state value ->
            if first then first <- false; value
            else reduction state value) Unchecked.defaultof<_> source
    let reduceBack reduction source =
        if isEmpty source then invalidArg "source" "The input list was empty."
        let mutable first = true
        foldBack (fun value state ->
            if first then first <- false; value
            else reduction value state) source Unchecked.defaultof<_>
    let min source = reduce (fun a b -> if b < a then b else a) source
    let max source = reduce (fun a b -> if b > a then b else a) source
    let private extremumBy better projection source =
        if isEmpty source then invalidArg "source" "The input sequence was empty."
        let mutable first = true
        let mutable best = Unchecked.defaultof<_>
        let mutable key = Unchecked.defaultof<_>
        iter (fun value ->
            let candidate = projection value
            if first || better candidate key then
                first <- false
                best <- value
                key <- candidate) source
        best
    let minBy projection source = extremumBy (<) projection source
    let maxBy projection source = extremumBy (>) projection source
    let private slice start count (source: CacheList<'T>) =
        if start = 0 && count = source.Length then source
        else CacheList(Tree.slice start count source.Root)
    let take count source =
        if count < 0 then invalidArg "count" "The input must be non-negative."
        if count > length source then invalidOp "The input sequence has an insufficient number of elements."
        slice 0 count source
    let skip count source =
        let count = Operators.max 0 count
        if count > length source then raise (ArgumentOutOfRangeException("count"))
        slice count (length source - count) source
    let truncate count source = slice 0 (Operators.max 0 (Operators.min count (length source))) source
    let private prefixLength predicate (source: CacheList<'T>) =
        let mutable count = 0
        Tree.tryPick (fun value ->
            if predicate value then count <- count + 1; None
            else Some ()) source.Root |> ignore
        count
    let takeWhile predicate source = take (prefixLength predicate source) source
    let skipWhile predicate source = skip (prefixLength predicate source) source
    let splitAt index source =
        if index < 0 then invalidArg "index" "Index outside collection."
        if index > length source then invalidOp "The input sequence has an insufficient number of elements."
        slice 0 index source, slice index (length source - index) source
    let partition predicate source =
        let yes, no = TreeBuilder<_>(), TreeBuilder<_>()
        iter (fun value -> if predicate value then yes.Add value else no.Add value) source
        CacheList(yes.Finish()), CacheList(no.Finish())
    // Sorting needs a contiguous working buffer; there is no intermediate list.
    let sortWith comparer source =
        let values = toArray source
        Tree.stableSortInPlaceWith comparer values
        ofArray values
    let sort source = sortWith compare source
    let private sortByWith comparer projection source =
        let values = Array.zeroCreate (length source)
        iteri (fun i value -> values[i] <- struct (projection value, value)) source
        Tree.stableSortInPlaceWith (fun struct (a,_) struct (b,_) -> comparer a b) values
        init values.Length (fun i -> let struct (_,value) = values[i] in value)
    let sortBy projection source = sortByWith compare projection source
    let sortDescending source = sortWith (fun a b -> compare b a) source
    let sortByDescending projection source =
        sortByWith (fun a b -> compare b a) projection source
    let distinctBy projection source =
        let seen = HashSet<_>(HashIdentity.Structural)
        filter (fun value -> seen.Add(projection value)) source
    let distinct source = distinctBy id source
    let except (exclusions: seq<'T>) source =
        if isEmpty source then source else
        let seen = HashSet<_>(exclusions, HashIdentity.Structural)
        filter seen.Add source
    let indexed source = mapi (fun i x -> i, x) source
    let pairwise source =
        let builder = TreeBuilder<_>()
        let mutable previous = Unchecked.defaultof<_>
        let mutable first = true
        iter (fun value ->
            if not first then builder.Add(previous, value)
            first <- false
            previous <- value) source
        CacheList(builder.Finish())
    let windowed size source =
        if size <= 0 then invalidArg "size" "The input must be positive."
        init (Operators.max 0 (length source - size + 1)) (fun i -> slice i size source)
    let chunkBySize size source =
        if size <= 0 then invalidArg "size" "The input must be positive."
        let n = length source
        init (if n = 0 then 0 else 1 + (n - 1) / size) (fun i ->
            let start = i * size
            slice start (Operators.min size (n - start)) source)
    let splitInto count source =
        if count <= 0 then invalidArg "count" "The input must be positive."
        let n = length source
        let parts = Operators.min count n
        if parts = 0 then empty else
        let small, extra = n / parts, n % parts
        init parts (fun i -> slice (i * small + Operators.min i extra) (small + (if i < extra then 1 else 0)) source)
    let private keyIndices<'Key when 'Key: equality> () =
        let keys = HashIdentity.Structural<'Key>
        let comparer =
            { new IEqualityComparer<struct ('Key * unit)> with
                member _.Equals(struct (a,_), struct (b,_)) = keys.Equals(a,b)
                member _.GetHashCode(struct (key,_)) = keys.GetHashCode key }
        Dictionary<struct ('Key * unit), int>(comparer)
    let groupBy projection source =
        // Wrapping keys in a struct tuple also supports null keys.
        let indices = keyIndices ()
        let keys = ResizeArray<_>()
        let groups = ResizeArray<TreeBuilder<_>>()
        iter (fun value ->
            let key = projection value
            let wrapped = struct (key, ())
            match indices.TryGetValue wrapped with
            | true, index -> groups[index].Add value
            | _ ->
                indices.Add(wrapped, keys.Count)
                keys.Add key
                let group = TreeBuilder<_>()
                group.Add value
                groups.Add group) source
        init keys.Count (fun i -> keys[i], CacheList(groups[i].Finish()))
    let countBy projection source =
        let indices = keyIndices ()
        let keys = ResizeArray<_>()
        let counts = ResizeArray<int>()
        iter (fun value ->
            let key = projection value
            let wrapped = struct (key, ())
            match indices.TryGetValue wrapped with
            | true, index -> counts[index] <- counts[index] + 1
            | _ ->
                indices.Add(wrapped, keys.Count)
                keys.Add key
                counts.Add 1) source
        init keys.Count (fun i -> keys[i], counts[i])
    let private differentLengths () = invalidArg "source" "The collections have different lengths."
    let mapi2 mapping (a: CacheList<'T>) (b: CacheList<'U>) =
        use other = (toSeq b).GetEnumerator()
        let result = mapi (fun i x ->
            if not (other.MoveNext()) then differentLengths ()
            mapping i x other.Current) a
        if other.MoveNext() then differentLengths ()
        result
    let map2 mapping a b = mapi2 (fun _ x y -> mapping x y) a b
    let map3 mapping (a: CacheList<'T>) b c =
        use second = (toSeq b).GetEnumerator()
        use third = (toSeq c).GetEnumerator()
        let result = map (fun x ->
            if not (second.MoveNext()) || not (third.MoveNext()) then differentLengths ()
            mapping x second.Current third.Current) a
        if second.MoveNext() || third.MoveNext() then differentLengths ()
        result
    let zip left right = map2 (fun a b -> a,b) left right
    let zip3 a b c = map3 (fun x y z -> x,y,z) a b c
    let unzip (source: CacheList<'T * 'U>) =
        let a,b = Tree.unzip source.Root
        CacheList a, CacheList b
    let unzip3 (source: CacheList<'T * 'U * 'V>) =
        let a,b,c = Tree.unzip3 source.Root
        CacheList a, CacheList b, CacheList c
    let fold2 folder state a b =
        use other = (toSeq b).GetEnumerator()
        let result = fold (fun state x ->
            if not (other.MoveNext()) then differentLengths ()
            folder state x other.Current) state a
        if other.MoveNext() then differentLengths ()
        result
    let iter2 action a b = fold2 (fun () x y -> action x y) () a b
    let iteri2 action a b =
        let mutable index = -1
        iter2 (fun x y -> index <- index + 1; action index x y) a b
    let private tryPair predicate a b =
        use first = (toSeq a).GetEnumerator()
        use second = (toSeq b).GetEnumerator()
        let mutable found = false
        let mutable running = true
        while running do
            let x,y = first.MoveNext(), second.MoveNext()
            if x <> y then differentLengths ()
            if not x then running <- false
            elif predicate first.Current second.Current then
                found <- true
                running <- false
        found
    let exists2 predicate a b = tryPair predicate a b
    let forall2 predicate a b = not (tryPair (fun x y -> not (predicate x y)) a b)
    let foldBack2 folder (a: CacheList<'T>) (b: CacheList<'U>) state =
        if a.Length <> b.Length then differentLengths ()
        use cursor = new TreeEnumerator<_>(b.Root, reverse = true) :> IEnumerator<_>
        foldBack (fun x state ->
            cursor.MoveNext() |> ignore
            folder x cursor.Current state) a state
    let scan folder state source =
        if length source = Int32.MaxValue then invalidOp "Collection is too large."
        let builder = TreeBuilder<_>()
        builder.Add state
        fold (fun state value -> let next = folder state value in builder.Add next; next) state source |> ignore
        CacheList(builder.Finish())
    let scanBack folder (source: CacheList<'T>) state =
        if source.Length = Int32.MaxValue then invalidOp "Collection is too large."
        let mutable current = state
        let mapped = CacheList(Tree.mapBack (fun value -> current <- folder value current; current) source.Root)
        add state mapped
    let mapFold mapping state source =
        let mutable current = state
        let result = map (fun value -> let mapped, next = mapping current value in current <- next; mapped) source
        result, current
    let mapFoldBack mapping (source: CacheList<'T>) state =
        let mutable current = state
        let result = CacheList(Tree.mapBack (fun value -> let mapped, next = mapping value current in current <- next; mapped) source.Root)
        result, current
    let allPairs a b =
        if int64 (length a) * int64 (length b) > int64 Int32.MaxValue then invalidOp "Collection is too large."
        if isEmpty a || isEmpty b then empty else
        let builder = TreeBuilder<_>()
        iter (fun x -> iter (fun y -> builder.Add(x,y)) b) a
        CacheList(builder.Finish())
    let permute mapping source =
        let n = length source
        let values = Array.zeroCreate n
        let assigned = Array.zeroCreate<bool> n
        let mutable valid = true
        iteri (fun i value ->
            let target = mapping i
            if target < 0 || target >= n then invalidArg "mapping" "The function did not compute a permutation."
            if assigned[target] then valid <- false
            assigned[target] <- true
            values[target] <- value) source
        if not valid then invalidArg "mapping" "The function did not compute a permutation."
        ofArray values
    let unfold generator state =
        let builder = TreeBuilder<_>()
        let mutable current = state
        let mutable running = true
        while running do
            match generator current with
            | Some (value, next) -> builder.Add value; current <- next
            | None -> running <- false
        CacheList(builder.Finish())
    let compareWith comparer a b =
        use first = (toSeq a).GetEnumerator()
        use second = (toSeq b).GetEnumerator()
        let mutable result = 0
        let mutable running = true
        while running do
            let x,y = first.MoveNext(), second.MoveNext()
            if not x || not y then
                result <- compare x y
                running <- false
            else
                result <- comparer first.Current second.Current
                running <- result = 0
        result
    let tryFindBack predicate (source: CacheList<'T>) =
        Tree.tryPickBack (fun x -> if predicate x then Some x else None) source.Root
    let findBack predicate source =
        match tryFindBack predicate source with Some x -> x | None -> raise (KeyNotFoundException())
    let tryFindIndexBack predicate (source: CacheList<'T>) =
        let mutable index = source.Length
        Tree.tryPickBack (fun x -> index <- index - 1; if predicate x then Some index else None) source.Root
    let findIndexBack predicate source =
        match tryFindIndexBack predicate source with Some i -> i | None -> raise (KeyNotFoundException())
    let tryExactlyOne source = if length source = 1 then Some (head source) else None
    let exactlyOne source =
        if length source <> 1 then invalidArg "source" "The input does not contain exactly one element."
        head source
    let insertManyAt index values source =
        if index < 0 || index > length source then invalidArg "index" "Index outside collection."
        let inserted = ofSeq values
        let left,right = splitAt index source
        append (append left inserted) right
    let removeManyAt index count source =
        if index < 0 then invalidArg "index" "Index outside collection."
        if int64 index + int64 count > int64 (length source) then invalidArg "count" "Count is outside collection."
        if count <= 0 then source else
        append (slice 0 index source) (slice (index + count) (length source - index - count) source)
    let transpose (sources: seq<CacheList<'T>>) =
        let rows = ResizeArray<_>(sources)
        if rows.Count = 0 then empty else
        let width = length rows[0]
        for row in rows do
            if length row <> width then differentLengths ()
        let cursors = Array.init rows.Count (fun i -> (toSeq rows[i]).GetEnumerator())
        try
            init width (fun _ ->
                init rows.Count (fun row ->
                    cursors[row].MoveNext() |> ignore
                    cursors[row].Current))
        finally
            for cursor in cursors do cursor.Dispose()
