module DiscogsWatcher.ListingChecker

open FSharp.Data
open FSharp.Data.Sql
open System
open FSharp.Control
open DiscogsWatcher.Configs

[<Literal>]
let DiscogsWantlistUri = "https://api.discogs.com/users/" + DiscogsUser + "/wants?per_page=100&token=" + DiscogsToken

[<Literal>]
let BaseListingUri = "https://api.discogs.com/marketplace/search?release_id="

[<Literal>]
let ListingSample = BaseListingUri + "1"

type Listings = JsonProvider<ListingSample>
type MyWantlist = JsonProvider<DiscogsWantlistUri>
type Db = SqlDataProvider<ConnectionString = ConnectionString>

let listingUri id = sprintf "%s%d&token=%s" BaseListingUri id DiscogsToken

let loadListing uri = async {
    try
        return! Listings.AsyncLoad uri
    with _ ->
        return [| |] }

let getPrice (l: Listings.Root) =
    match l.Price.Number with
    | Some p -> p
    | None -> l.Price.String.Value.[3..] |> Decimal.Parse

let checkForNewListings () = async {
    let ctx = Db.GetDataContext()

    let listingsAlreadySeen = query {
        for listing in ctx.Dbo.Listings do 
        select listing.ListingId } |> Set.ofSeq

    let! wantlist = MyWantlist.AsyncGetSample()
    let releasesInWantlist =
        wantlist.Wants
        |> Seq.choose (fun r -> match r.Notes with Some p -> Some (r.Id, decimal p) | _ -> None)
        |> dict

    let! allListings =
        releasesInWantlist.Keys
        |> Seq.map (listingUri >> loadListing)
        |> Async.Parallel

    let cheapListings =
        allListings
        |> Array.concat
        |> Array.filter (fun l -> listingsAlreadySeen.Contains l.Id |> not && getPrice l <= releasesInWantlist.[l.ReleaseId])
        |> Array.toList

    cheapListings
    |> List.iter (fun l -> let n = ctx.Dbo.Listings.Create(l.Currency, getPrice l, l.ReleaseId, l.ShipsFrom)
                           n.ListingId <- l.Id)
    ctx.SubmitUpdates ()
    return cheapListings |> List.map (fun l -> l.Id) }

let cheapListingChecker: MailboxProcessor<AsyncReplyChannel<_>> =
    MailboxProcessor.Start(fun inbox ->
        let rec loop () = async {
            let! msg = inbox.Receive ()
            let! newListings = checkForNewListings ()
            msg.Reply newListings
            return! loop () }
        loop ())