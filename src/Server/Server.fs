module Blockchain.Server

open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Shared
open Blockchain.Mqtt
open System.Text
open System.Threading
open System
open MQTTnet.Channel
open MQTTnet.Client

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"
let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let getInitCounter () : Task<Counter> = task { return { Value = 42 } }
let webApp =
    route "/api/init" >=>
        fun next ctx ->
            task {
                let! counter = getInitCounter()
                return! Successful.OK counter next ctx
            }

let configureApp (app : IApplicationBuilder) =
    app.UseDefaultFiles()
       .UseStaticFiles()
       .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore
    services.AddSingleton<Giraffe.Serialization.Json.IJsonSerializer>(Thoth.Json.Giraffe.ThothSerializer()) |> ignore

let webHost () =
    WebHost
        .CreateDefaultBuilder()
        .UseWebRoot(publicPath)
        .UseContentRoot(publicPath)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .UseUrls("http://0.0.0.0:" + port.ToString() + "/")
        .Build()
        .Run()

let  mqttExample (client : IMqttClient) =
    let myTopic = sprintf "client/%s" client.Options.ClientId
    mqttSubscribe client "tx"
    mqttSubscribe client "block"
    mqttSubscribe client "join"
    mqttSubscribe client myTopic
    client.ApplicationMessageReceived.Add (fun x ->
        let topic = x.ApplicationMessage.Topic
        let payload = x.ApplicationMessage.Payload |> Encoding.UTF8.GetString
        match topic with
        | "join" ->
            let who = payload
            let myUrl = sprintf "http://localhost:%d" port
            mqttSendClient client who myUrl
        | _ -> printfn "mqtt: %s: %s" topic payload
    )
    Async.Sleep Timeout.Infinite

[<EntryPoint>]
let main argv =
    let client = mqttConnect "localhost"
    try
        mqttExample client |> Async.RunSynchronously
        AppDomain.CurrentDomain.ProcessExit.Add (fun _ -> exit 0)
        // printfn "Press enter to exit."
        // Console.Read () |> ignore
        // webHost ()
    finally
        mqttDisconnect client
    0