﻿module Blockchain.Broker

open System
open MQTTnet
open MQTTnet.Client
open MQTTnet.Server
open System.Threading


let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let listenAddress =
    "MQTT_SERVER_ADDRESS"
    |> tryGetEnv |> Option.defaultValue "127.0.0.1"

let mqttBroker () =
    let broker = MqttFactory().CreateMqttServer()
    let opts =
        MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .Build()
    broker.StartAsync opts
    |> Async.AwaitTask
    |> fun t -> async {
        do! t
        return! Async.Sleep Timeout.Infinite
    }

[<EntryPoint>]
let main argv =
    printfn "Starting MQTT broker"
    mqttBroker () |> Async.RunSynchronously
    AppDomain.CurrentDomain.ProcessExit.Add (fun _ -> exit 0)
    // printfn "Press enter to exit."
    // Console.Read () |> ignore
    0 // return an integer exit code
