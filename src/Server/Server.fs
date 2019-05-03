open System
open System.IO
open System.Threading.Tasks
open System.Security.Cryptography

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

open FSharp.Control.Tasks.V2
open Giraffe
open Shared
open MQTTnet
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

let exportPrivateKey (rsa : RSA) =
    let p = rsa.ExportParameters true
    let param = [
        p.Modulus
        p.Exponent
        p.D
        p.P
        p.Q
        p.DP
        p.DQ
        p.InverseQ
    ]
    List.map Convert.ToBase64String param

let  doMqtt () =
    let fact = MqttFactory()
    let client = fact.CreateMqttClient()
    let opts =
        MqttClientOptionsBuilder()
            .WithTcpServer("localhost", Nullable 1883)
            .Build()
    client.ConnectAsync opts |> Async.AwaitTask |> Async.RunSynchronously |> ignore
    printfn "%A" client.IsConnected
    let msg =
        MqttApplicationMessageBuilder()
            .WithTopic("all")
            .WithPayload("hello")
            .Build()
    client.ApplicationMessageReceived.Add (fun x -> printfn "%A" x.ApplicationMessage.Payload)
    client.Connected.Subscribe (fun x -> printfn "%A" x.IsSessionPresent) |> ignore
    client.SubscribeAsync
        [TopicFilter ("all", Protocol.MqttQualityOfServiceLevel.AtLeastOnce)]
    |> Async.AwaitTask |> fun y ->
        async {
            let! x = y
            x |> Seq.iter (fun m -> printfn "%A" m.TopicFilter.Topic)
            return ()
        } |> Async.Start
    client.PublishAsync msg |> Async.AwaitTask |> Async.RunSynchronously
    client.PublishAsync msg |> Async.AwaitTask |> Async.RunSynchronously
    client.PublishAsync msg |> Async.AwaitTask |> Async.RunSynchronously
    client.DisconnectAsync () |> Async.AwaitTask |> Async.RunSynchronously
    ()

[<EntryPoint>]
let main argv =
    let rsa = RSA.Create()
    let d1 = Text.Encoding.UTF8.GetBytes "foo"
    let d2 = Text.Encoding.UTF8.GetBytes "goo"
    let s = rsa.SignData (d1, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    printfn "%A" (Convert.ToBase64String s)
    let v = rsa.VerifyData (d1, s, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    let v' = rsa.VerifyData (d2, s, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    printfn "%A" (v, v')
    doMqtt ()
    // webHost ()
    0