module Blockchain.Broker

open System
open MQTTnet
open MQTTnet.Client
open MQTTnet.Server


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
    broker.StartAsync opts |> Async.AwaitTask 

[<EntryPoint>]
let main argv =
    printfn "Starting MQTT broker"
    mqttBroker () |> Async.Start
    printfn "Press enter to exit."
    Console.Read () |> ignore
    0 // return an integer exit code
