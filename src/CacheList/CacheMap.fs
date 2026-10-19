namespace CacheCollections

open System
open System.Collections
open System.Collections.Generic

// Owned arrays are never changed after a node is published. Leaves hold all entries;
// branches hold inclusive upper bounds and an O(1) subtree count.
type internal MapNode<'K,'V> =
    | MapLeaf of keys: 'K array * values: 'V array
    | MapBranch of children: MapNode<'K,'V> array * upperBounds: 'K array * count: int

module internal MapTree =
    let width = 32
    let minimum = 16
    let count = function MapLeaf(k,_) -> k.Length | MapBranch(_,_,n) -> n
    let arity = function MapLeaf(k,_) -> k.Length | MapBranch(c,_,_) -> c.Length
    let rec maximum = function
        | MapLeaf(k,_) -> k[k.Length-1]
        | MapBranch(_,k,_) -> k[k.Length-1]
    let branch children =
        let mutable total = 0L
        let keys = Array.map (fun c -> total <- total + int64(count c); maximum c) children
        if total > int64 Int32.MaxValue then raise (OverflowException())
        MapBranch(children, keys, int total)
    let lowerBound (cmp: IComparer<'K>) key (keys: 'K array) =
        let mutable lo = 0
        let mutable hi = keys.Length
        while lo < hi do
            let mid = lo + (hi-lo)/2
            if cmp.Compare(keys[mid],key) < 0 then lo <- mid+1 else hi <- mid
        lo
    let rec tryGet (cmp: IComparer<'K>) key node (value: byref<'V>) =
        match node with
        | MapLeaf(k,v) ->
            let i = lowerBound cmp key k
            if i < k.Length && cmp.Compare(k[i],key)=0 then value <- v[i]; true else false
        | MapBranch(c,k,_) ->
            let i = lowerBound cmp key k
            i < c.Length && tryGet cmp key c[i] &value
    let splice i remove (insert: 'T array) (source: 'T array) =
        let result = Array.zeroCreate (source.Length - remove + insert.Length)
        Array.Copy(source,0,result,0,i)
        Array.Copy(insert,0,result,i,insert.Length)
        Array.Copy(source,i+remove,result,i+insert.Length,source.Length-i-remove)
        result
    let replace i value (a: 'T array) =
        let result = Array.copy a
        result[i] <- value
        result
    let split = function
        | MapLeaf(k,v) ->
            let n = k.Length/2
            struct(MapLeaf(k[..n-1],v[..n-1]), MapLeaf(k[n..],v[n..]))
        | MapBranch(c,_,_) ->
            let n = c.Length/2
            struct(branch c[..n-1],branch c[n..])
    let rec add cmp key value node =
        let updated =
            match node with
            | MapLeaf(k,v) ->
                let i = lowerBound cmp key k
                if i < k.Length && cmp.Compare(k[i],key)=0 then
                    // F# Map replaces the representative key as well as its value.
                    MapLeaf(replace i key k,replace i value v)
                else MapLeaf(splice i 0 [|key|] k,splice i 0 [|value|] v)
            | MapBranch(c,k,_) ->
                let i = min (c.Length-1) (lowerBound cmp key k)
                let struct(left,right) = add cmp key value c[i]
                match right with
                | None -> branch (replace i left c)
                | Some r -> branch (splice i 1 [|left;r|] c)
        if arity updated <= width then struct(updated,None)
        else let struct(l,r) = split updated in struct(l,Some r)
    let combine left right =
        match left,right with
        | MapLeaf(k,v),MapLeaf(l,w) -> MapLeaf(Array.append k l, Array.append v w)
        | MapBranch(c,_,_),MapBranch(d,_,_) -> branch (Array.append c d)
        | _ -> invalidOp "Unequal tree depths."
    let rec remove cmp key node =
        match node with
        | MapLeaf(k,v) ->
            let i = lowerBound cmp key k
            if i=k.Length || cmp.Compare(k[i],key)<>0 then node
            else MapLeaf(splice i 1 [||] k,splice i 1 [||] v)
        | MapBranch(c,k,_) ->
            let i = lowerBound cmp key k
            if i=c.Length then node
            else
                let updated = remove cmp key c[i]
                if Object.ReferenceEquals(updated,c[i]) then node
                elif arity updated >= minimum || c.Length=1 then branch (replace i updated c)
                else
                    let first = if i=0 then 0 else i-1
                    let left,right = if i=0 then updated,c[1] else c[first],updated
                    let merged = combine left right
                    if arity merged <= width then branch (splice first 2 [|merged|] c)
                    else
                        let struct(l,r) = split merged
                        branch (splice first 2 [|l;r|] c)
    let rec normalize = function
        | MapBranch(c,_,_) when c.Length=1 -> normalize c[0]
        | node -> node
    let rec fold folder state node =
        let mutable s = state
        match node with
        | MapLeaf(k,v) -> for i=0 to k.Length-1 do s <- folder s k[i] v[i]
        | MapBranch(c,_,_) -> for child in c do s <- fold folder s child
        s
    let rec foldBack folder node state =
        let mutable s = state
        match node with
        | MapLeaf(k,v) -> for i=k.Length-1 downto 0 do s <- folder k[i] v[i] s
        | MapBranch(c,_,_) -> for i=c.Length-1 downto 0 do s <- foldBack folder c[i] s
        s
    let rec map mapping = function
        | MapLeaf(k,v) -> MapLeaf(k,Array.mapi (fun i value -> mapping k[i] value) v)
        | MapBranch(c,k,n) -> MapBranch(Array.map (map mapping) c,k,n)
    let rec tryPick chooser = function
        | MapLeaf(k,v) ->
            let mutable result = None
            let mutable i = 0
            while i < k.Length && Option.isNone result do
                result <- chooser k[i] v[i]
                i <- i+1
            result
        | MapBranch(c,_,_) ->
            let mutable result = None
            let mutable i = 0
            while i < c.Length && Option.isNone result do
                result <- tryPick chooser c[i]
                i <- i+1
            result
    // Even groups avoid an underfull final node at every level.
    let groups length = if length=0 then 0 else 1+(length-1)/width
    let pack (keys: 'K array) (values: 'V array) =
        if keys.Length=0 then MapLeaf([||],[||])
        else
            let groupCount = groups keys.Length
            let mutable offset = 0
            let mutable nodes = Array.init groupCount (fun i ->
                let size = keys.Length/groupCount + (if i < keys.Length%groupCount then 1 else 0)
                let node = MapLeaf(keys[offset..offset+size-1],values[offset..offset+size-1])
                offset <- offset+size
                node)
            while nodes.Length>1 do
                let n = groups nodes.Length
                let mutable start = 0
                nodes <- Array.init n (fun i ->
                    let size = nodes.Length/n + (if i < nodes.Length%n then 1 else 0)
                    let node = branch nodes[start..start+size-1]
                    start <- start+size
                    node)
            nodes[0]
    let ofArray (cmp: IComparer<'K>) (source: ('K*'V) array) =
        // Sort original positions as a tie-breaker: the final equal key wins,
        // including its representative, without depending on sort stability.
        let mutable sorted = true
        let mutable i = 1
        while sorted && i<source.Length do
            sorted <- cmp.Compare(fst source[i-1],fst source[i]) <= 0
            i <- i+1
        let order = Array.init source.Length id
        if not sorted then
            Array.sortInPlaceWith (fun a b ->
                let c = cmp.Compare(fst source[a],fst source[b])
                if c=0 then compare a b else c) order
        let keys = ResizeArray<'K>()
        let values = ResizeArray<'V>()
        for i in order do
            let key,value = source[i]
            let last = keys.Count-1
            if last>=0 && cmp.Compare(keys[last],key)=0 then
                keys[last] <- key
                values[last] <- value
            else keys.Add key; values.Add value
        pack (keys.ToArray()) (values.ToArray())

type internal MapEnumerator<'K,'V>(root: MapNode<'K,'V>) =
    let pending = Stack<MapNode<'K,'V>>()
    let mutable keys = [||]
    let mutable values = [||]
    let mutable index = -1
    let mutable valid = false
    do pending.Push root
    member private _.Current =
        if not valid then invalidOp "Enumeration has not started or has finished."
        KeyValuePair(keys[index],values[index])
    interface IEnumerator<KeyValuePair<'K,'V>> with
        member this.Current = this.Current
        member _.Dispose() = pending.Clear(); keys <- [||]; values <- [||]; valid <- false
    interface IEnumerator with
        member this.Current = box this.Current
        member _.Reset() = raise (NotSupportedException())
        member _.MoveNext() =
            index <- index+1
            valid <- index<keys.Length
            while not valid && pending.Count>0 do
                match pending.Pop() with
                | MapLeaf(k,v) -> keys <- k; values <- v; index <- 0; valid <- k.Length>0
                | MapBranch(c,_,_) -> for i=c.Length-1 downto 0 do pending.Push c[i]
            valid

/// An ordered, persistent map with packed leaves and path-copying edits.
[<Sealed>]
type CacheMap<'Key,'Value when 'Key: comparison> internal (root: MapNode<'Key,'Value>) =
    static let comparer = LanguagePrimitives.FastGenericComparer<'Key>
    member internal _.Root = root
    member _.Count = MapTree.count root
    member this.IsEmpty = this.Count=0
    member _.TryGetValue(key, [<System.Runtime.InteropServices.Out>] value: byref<'Value>) = MapTree.tryGet comparer key root &value
    member this.TryFind key =
        let mutable value = Unchecked.defaultof<'Value>
        if this.TryGetValue(key,&value) then Some value else None
    member this.ContainsKey key =
        let mutable value = Unchecked.defaultof<'Value>
        this.TryGetValue(key,&value)
    member this.Item
        with get key =
            let mutable value = Unchecked.defaultof<'Value>
            if this.TryGetValue(key,&value) then value else raise (KeyNotFoundException())
    member _.Add(key,value) =
        let struct(left,right) = MapTree.add comparer key value root
        CacheMap(match right with None -> left | Some r -> MapTree.branch [|left;r|])
    member this.Remove key =
        let updated = MapTree.remove comparer key root
        if Object.ReferenceEquals(root,updated) then this else CacheMap(MapTree.normalize updated)
    member private this.Entries = this :> IEnumerable<KeyValuePair<'Key,'Value>>
    override this.Equals other =
        match other with
        | :? CacheMap<'Key,'Value> as rhs when this.Count=rhs.Count ->
            use left = this.Entries.GetEnumerator()
            use right = rhs.Entries.GetEnumerator()
            let mutable equal = true
            while equal && left.MoveNext() do
                equal <- right.MoveNext() && Unchecked.equals left.Current.Key right.Current.Key
                         && Unchecked.equals left.Current.Value right.Current.Value
            equal
        | _ -> false
    override _.GetHashCode() =
        MapTree.fold (fun hash key value -> (hash*31 + Unchecked.hash key)*31 + Unchecked.hash value) 0 root
    interface IComparable with
        member this.CompareTo other =
            match other with
            | :? CacheMap<'Key,'Value> as rhs ->
                use left = this.Entries.GetEnumerator()
                use right = rhs.Entries.GetEnumerator()
                let mutable result = 0
                let mutable running = true
                while running do
                    let l,r = left.MoveNext(),right.MoveNext()
                    if not l || not r then result <- compare l r; running <- false
                    else
                        result <- comparer.Compare(left.Current.Key,right.Current.Key)
                        if result=0 then result <- Unchecked.compare left.Current.Value right.Current.Value
                        running <- result=0
                result
            | _ -> invalidArg "other" "Expected a CacheMap with the same key and value types."
    interface IEnumerable<KeyValuePair<'Key,'Value>> with
        member _.GetEnumerator() = new MapEnumerator<'Key,'Value>(root) :> IEnumerator<_>
    interface IEnumerable with
        member _.GetEnumerator() = new MapEnumerator<'Key,'Value>(root) :> IEnumerator
    interface IReadOnlyCollection<KeyValuePair<'Key,'Value>> with
        member this.Count = this.Count
    interface IReadOnlyDictionary<'Key,'Value> with
        member this.Item with get key = this[key]
        member this.ContainsKey key = this.ContainsKey key
        member this.TryGetValue(key,value) = this.TryGetValue(key,&value)
        member this.Keys = this.Entries |> Seq.map (fun entry -> entry.Key)
        member this.Values = this.Entries |> Seq.map (fun entry -> entry.Value)

/// Operations on ordered persistent maps. foldBack and partition visit descending keys.
[<RequireQualifiedAccess>]
module CacheMap =
    let empty<'Key,'Value when 'Key: comparison> : CacheMap<'Key,'Value> = CacheMap(MapLeaf([||],[||]))
    let isEmpty (source: CacheMap<_,_>) = source.IsEmpty
    let count (source: CacheMap<_,_>) = source.Count
    let add key value (source: CacheMap<_,_>) = source.Add(key,value)
    let remove key (source: CacheMap<_,_>) = source.Remove key
    let containsKey key (source: CacheMap<_,_>) = source.ContainsKey key
    let tryFind key (source: CacheMap<_,_>) = source.TryFind key
    let find key (source: CacheMap<_,_>) = source[key]
    let change key updater (source: CacheMap<_,_>) =
        match updater (source.TryFind key) with
        | Some value -> source.Add(key,value)
        | None -> source.Remove key
    let ofArray (source: ('Key*'Value) array) =
        if isNull source then nullArg "source"
        CacheMap(MapTree.ofArray LanguagePrimitives.FastGenericComparer<'Key> source)
    let ofSeq source = ofArray (Seq.toArray source)
    let ofList source = ofArray (List.toArray source)
    let toArray (source: CacheMap<_,_>) =
        let result = Array.zeroCreate source.Count
        MapTree.fold (fun i k v -> result[i] <- k,v; i+1) 0 source.Root |> ignore
        result
    let toList (source: CacheMap<_,_>) = MapTree.foldBack (fun k v s -> (k,v)::s) source.Root []
    let toSeq (source: CacheMap<_,_>) = source |> Seq.map (fun e -> e.Key,e.Value)
    let keys (source: CacheMap<_,_>) = (source :> IReadOnlyDictionary<_,_>).Keys
    let values (source: CacheMap<_,_>) = (source :> IReadOnlyDictionary<_,_>).Values
    let fold folder state (source: CacheMap<_,_>) = MapTree.fold folder state source.Root
    let foldBack folder (source: CacheMap<_,_>) state = MapTree.foldBack folder source.Root state
    let iter action source = fold (fun () k v -> action k v) () source
    let map mapping (source: CacheMap<_,_>) = CacheMap(MapTree.map mapping source.Root)
    let tryPick chooser (source: CacheMap<_,_>) = MapTree.tryPick chooser source.Root
    let pick chooser source =
        match tryPick chooser source with Some value -> value | None -> raise (KeyNotFoundException())
    let tryFindKey predicate source = tryPick (fun k v -> if predicate k v then Some k else None) source
    let findKey predicate source = pick (fun k v -> if predicate k v then Some k else None) source
    let exists predicate source = tryFindKey predicate source |> Option.isSome
    let forall predicate source = not (exists (fun k v -> not (predicate k v)) source)
    let filter predicate (source: CacheMap<_,_>) =
        let k,v = ResizeArray(),ResizeArray()
        iter (fun key value -> if predicate key value then k.Add key; v.Add value) source
        if k.Count=source.Count then source else CacheMap(MapTree.pack (k.ToArray()) (v.ToArray()))
    let partition predicate (source: CacheMap<_,_>) =
        let a,b,c,d = ResizeArray(),ResizeArray(),ResizeArray(),ResizeArray()
        // Match F# Map's descending predicate order, then pack ascending keys.
        MapTree.foldBack (fun k v () -> if predicate k v then a.Add k; b.Add v else c.Add k; d.Add v) source.Root ()
        let pack (keys: ResizeArray<_>) (values: ResizeArray<_>) =
            if keys.Count=source.Count then source
            else
                let k,v = keys.ToArray(),values.ToArray()
                Array.Reverse k
                Array.Reverse v
                CacheMap(MapTree.pack k v)
        pack a b,pack c d
    let minKeyValue (source: CacheMap<_,_>) =
        if source.IsEmpty then raise (KeyNotFoundException())
        let rec first = function MapLeaf(k,v) -> k[0],v[0] | MapBranch(c,_,_) -> first c[0]
        first source.Root
    let maxKeyValue (source: CacheMap<_,_>) =
        if source.IsEmpty then raise (KeyNotFoundException())
        let rec last = function
            | MapLeaf(k,v) -> k[k.Length-1],v[v.Length-1]
            | MapBranch(c,_,_) -> last c[c.Length-1]
        last source.Root
    let tryMinKeyValue source = if isEmpty source then None else Some(minKeyValue source)
    let tryMaxKeyValue source = if isEmpty source then None else Some(maxKeyValue source)
