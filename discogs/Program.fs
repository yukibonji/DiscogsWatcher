open FSharp.Data
open FSharp.Data.Sql
open System.Net
open System.Collections.Specialized
open System
open System.Text
open System.Threading

// configs

[<Literal>]
let DiscogsToken = "token"

[<Literal>]
let DiscogsUser = "user"

let mailgunUrl = "https://api.mailgun.net/v3/appXXXX.mailgun.org/messages"
let mailgunApiKey = "api:key-DSADSADSA"
let recipient = "adsadasd@live.com"

[<Literal>]
let ConnectionString = "CS" 

// -------

[<Literal>]
let DiscogsWantlistUri = "https://api.discogs.com/users/" + DiscogsUser + "/wants?per_page=100&token=" + DiscogsToken

[<Literal>]
let BaseListingUri = "https://api.discogs.com/marketplace/search?release_id="

[<Literal>]
let ListingSample = BaseListingUri + "1"

type Listings = JsonProvider<ListingSample>
type MyWantlist = JsonProvider<DiscogsWantlistUri>
type Db = SqlDataProvider<ConnectionString>

[<EntryPoint>]
let main argv = 
    let listingUri id = sprintf "%s%d&token=%s" BaseListingUri id DiscogsToken

    let loadListing (uri: string) =
        Thread.Sleep 3000 // let's not hit discogs API rate limits

        try
            Listings.Load uri
        with _ ->
            printfn "\nLoadListing failed"
            [| |]

    let getPrice (l: Listings.Root) =
        match l.Price.Number with
        | Some p -> p
        | None -> l.Price.String.Value.[3..] |> Decimal.Parse

    let getBody (listings: seq<Listings.Root>) =
        let m = listings |> Seq.map (fun l -> sprintf "http://www.discogs.com/sell/item/%d" l.Id)
        String.Join("\\n", m)

    let interval = argv.[0] |> Int32.Parse

    printfn "Starting, interval %d minutes" interval

    while true do
        let ctx = Db.GetDataContext()

        let dbListings = query {
            for listing in ctx.``[dbo].[Listings]`` do 
            select listing.ListingId } |> Set.ofSeq
    
        let toWatch =
            MyWantlist.GetSample().Wants
            |> Seq.map (fun r -> match r.Notes with Some p -> Some (r.Id, decimal p) | _ -> None)
            |> Seq.choose id
            |> dict
    
        let cheapListings =
            toWatch.Keys
            |> Seq.map (listingUri >> loadListing)
            |> Seq.collect (Seq.filter (fun l -> dbListings.Contains l.Id |> not && getPrice l <= toWatch.[l.ReleaseId]))
            |> Seq.toArray

        cheapListings
        |> Array.iter (fun l -> let n = ctx.``[dbo].[Listings]``.Create(l.Currency, getPrice l, l.ReleaseId, l.ShipsFrom)
                                n.ListingId <- l.Id)
        
        if cheapListings.Length > 0 then
            ctx.SubmitUpdates ()

            use wc = new WebClient ()
            wc.Headers.["Content-Type"] <- "application/x-www-form-urlencoded"
            wc.Headers.["Authorization"] <- "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(mailgunApiKey))

            let vals = NameValueCollection()
            vals.["to"] <- recipient
            vals.["subject"] <- "discogs watch"
            vals.["text"] <- getBody cheapListings
            vals.["from"] <- "Watcher" 

            wc.UploadValues (mailgunUrl, vals) |> ignore

            printfn "\n%d eligible listings found at %s" cheapListings.Length (DateTime.Now.ToString())
        printf "."
        Thread.Sleep (60 * 1000 * interval)
    0