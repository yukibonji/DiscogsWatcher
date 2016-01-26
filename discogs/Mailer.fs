module DiscogsWatcher.Mailer

open DiscogsWatcher.Configs
open System
open System.Net
open System.Text
open System.Collections.Specialized

let mailer =
    MailboxProcessor.Start(fun inbox ->
        let rec loop () = async {
            let! listingIds = inbox.Receive ()

            use wc = new WebClient ()
            wc.Headers.["Content-Type"] <- "application/x-www-form-urlencoded"
            wc.Headers.["Authorization"] <- "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(mailgunApiKey))

            let vals = NameValueCollection()
            vals.["from"] <- sender
            vals.["to"] <- recipient
            vals.["subject"] <- "New cheap listings"
            vals.["text"] <- listingIds |> List.map (sprintf "http://www.discogs.com/sell/item/%d") |> String.concat ("\\n")

            do! wc.UploadValuesTaskAsync (mailgunUrl, vals) |> Async.AwaitTask |> Async.Ignore
            return! loop () }
        loop ())