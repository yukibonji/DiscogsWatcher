module DiscogsWatcher.Program

open DiscogsWatcher.Dal
open System.Threading.Tasks
open Topshelf.FSharpApi
open Time

[<EntryPoint>]
let main _ =
    let timer = new System.Timers.Timer ((min 10).TotalMilliseconds, AutoReset = true)
    timer.Elapsed
    |> Observable.add (fun _ -> Task.Run checkForNewListings |> ignore)

    let start _ =
        timer.Start ()
        true

    let stop _ =
        timer.Dispose ()
        true

    Service.Default
    |> with_start start
    |> with_stop stop
    |> service_name "DiscogsWatcher"
    |> run