module Blockchain.Test

open System
open Fake.Core
open Fake.DotNet
open System.Threading
open Fake.SystemHelper
open System.Diagnostics

let getCmdArgs args =
    let x = String.splitStr "->" args
    let cmd = List.head x
    let args' = if List.length x > 1 then x.[1] else ""
    cmd, args'

let runTool args workingDir =
    let cmd, args' = getCmdArgs args
    let arguments = args' |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet args workingDir =
    let cmd, args' = getCmdArgs args
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd args'
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let runToolAsync args workingDir =
    async { return runTool args workingDir }

let runDotNetAsync args workingDir =
    async { return runDotNet args workingDir }

let runServer port args =
    let cmd = "../src/Server/bin/Debug/netcoreapp2.2/Server.dll"
    let env = Process.createEnvironmentMap ()
    let env' = Map.add "SERVER_PORT" (string port) env
    let opts = DotNet.Options.withEnvironment env'
    async { return DotNet.exec opts cmd args } |> Async.Ignore |> Async.Start

let runPoll args =
    runDotNet ("../src/Poll/bin/Debug/netcoreapp2.2/Poll.dll -> " + args) "."

[<EntryPoint>]
let main argv =
    runDotNetAsync "../src/Broker/bin/Debug/netcoreapp2.2/Broker.dll" "." |> Async.Start
    [ 0 .. 2 ] |> List.iter (fun i -> runServer (8085 + i) "")
    [ 1 .. 5 ] |> List.iter (fun i ->
        let cmd = sprintf "pubkey --generate key%d" i
        runPoll cmd 
    )
    Async.Sleep 4000 |> Async.RunSynchronously
    runPoll "test --all --key key1" 
    // runPoll "election --new election1.json --key key1" 
    // runPoll "election --new election2.json --key key2" 
    // runPoll "voter --new voter1.json --key key3" 
    // runPoll "voter --new voter2.json --key key4" 
    // runPoll "voter --new voter3.json --key key5"
    // runPoll "vote --cast vote1.json --voter voter1.json --key key3" 
    // runPoll "vote --cast vote2.json --voter voter2.json --key key4" 
    // runPoll "vote --cast vote3.json --voter voter3.json --key key5" 
    // runPoll "vote --cast vote1.json --voter voter1.json --key key4" 
    // runPoll "vote --cast vote1.json --voter voter2.json --key key3" 

    // ctrl-c triggers SIGINT on Linux, causing all child procs to exit
    printfn "Press ctrl-c to exit."
    Async.Sleep Timeout.Infinite |> Async.RunSynchronously
    0 // return an integer exit code
