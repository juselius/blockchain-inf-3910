module Blockchain.Test

open System
open System.Diagnostics
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNetArgs cmd args workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd (" -- " + args)
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let runDotNet cmd workingDir =
    runDotNetArgs cmd "" workingDir

let runToolAsync cmd args workingDir =
    async { return runTool cmd args workingDir }

let runDotNetAsync cmd args workingDir =
    async { return runDotNetArgs cmd args workingDir }

[<EntryPoint>]
let main argv =
    runDotNetAsync "run" "" "../src/Broker" |> Async.Start
    //Async.Sleep 2000 |> Async.RunSynchronously
    0 // return an integer exit code
