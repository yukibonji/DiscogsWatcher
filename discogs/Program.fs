module DiscogsWatcher.Program

open DiscogsWatcher.Mailer
open DiscogsWatcher.ListingChecker
open Topshelf.FSharpApi
open Time

let control =
    MailboxProcessor.Start(fun inbox ->
        let rec loop () = async {
            let! _ = inbox.Receive ()
            match cheapListingChecker.PostAndReply id with
            | [] -> ()
            | ids -> mailer.Post ids
            return! loop () }
        loop ())

[<EntryPoint>]
let main _ =
    let timer = new System.Timers.Timer ((min 15).TotalMilliseconds, AutoReset = true)
    timer.Elapsed
    |> Observable.add (fun _ -> control.Post ())

    let start _ =
        timer.Start ()
        control.Post ()
        true

    let stop _ =
        timer.Dispose ()
        true

    Service.Default
    |> with_start start
    |> with_stop stop
    |> service_name "DiscogsWatcher"
    |> run