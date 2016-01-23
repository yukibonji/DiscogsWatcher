module DiscogsWatcher.Program

open System
open System.Threading
open DiscogsWatcher.Dal

[<EntryPoint>]
let main argv = 
    let interval = argv.[0] |> Int32.Parse
    printfn "Starting, interval %d minutes" interval

    while true do
        checkForNewListings ()
        printf "."
        Thread.Sleep (60 * 1000 * interval)
    0