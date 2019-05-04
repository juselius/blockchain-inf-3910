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
open MQTTnet.Server


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

let mqttBroker () =
    let broker = MqttFactory().CreateMqttServer()
    let opts =
        MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .Build()
    broker.StartAsync opts |> Async.AwaitTask |> Async.Start

let mqttConnect server =
    let client = MqttFactory().CreateMqttClient()
    let opts =
        MqttClientOptionsBuilder()
            .WithTcpServer(server, Nullable 1883)
            .Build()
    client.ConnectAsync opts 
    |> Async.AwaitTask 
    |> Async.RunSynchronously 
    |> ignore
    printfn "mqtt connected: %A" client.IsConnected
    client

let mqttDisconnect (client : IMqttClient) =
    client.DisconnectAsync () 
    |> Async.AwaitTask 
    |> Async.RunSynchronously

let mqttSend (client : IMqttClient) topic (message : string) =
    let msg =
        MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithAtLeastOnceQoS()
            .WithPayload(message)
            .Build()
    client.PublishAsync msg |> Async.AwaitTask |> Async.Start

let mqttSendTransaction client trans = 
    mqttSend client "tx" trans

let mqttSubscribe (client : IMqttClient) (topics : string list) = 
    topics 
    |> List.map (fun t -> TopicFilter (t, Protocol.MqttQualityOfServiceLevel.AtLeastOnce))
    |> client.SubscribeAsync 
    |> Async.AwaitTask 
    |> Async.Ignore
    |> Async.Start

let  doMqtt () =
    let client = mqttConnect "localhost"
    client.ApplicationMessageReceived.Add (fun x -> 
        printfn "recv: %A" x.ApplicationMessage.Topic
        printfn "    : %A" x.ApplicationMessage.Payload
    )
    mqttSendTransaction client "new tx"
    mqttDisconnect client

let sha256 = System.Security.Cryptography.SHA256.Create()
let hash (n : int) =
        sha256.ComputeHash (BitConverter.GetBytes n) 
        |> BitConverter.ToString 
        |> fun x -> x.Replace ("-", "")

let verify x = hash x |> fun p1 -> p1.EndsWith "00000" 

let rec proofOfWork p0 x =
    if verify (p0 + x) then
        x
    else
        proofOfWork p0 (x + 1)
    
[<EntryPoint>]
let main argv =
    mqttBroker () 
    let rsa = RSA.Create()
    let d1 = Text.Encoding.UTF8.GetBytes "foo"
    let d2 = Text.Encoding.UTF8.GetBytes "goo"
    let s = rsa.SignData (d1, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    printfn "%A" (Convert.ToBase64String s)
    let v = rsa.VerifyData (d1, s, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    let v' = rsa.VerifyData (d2, s, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    let proof = proofOfWork 12345 0
    let sha = hash (12345 + proof)
    printfn "PoW = %A %A" proof sha
    printfn "%A" (v, v')
    doMqtt ()
    // webHost ()
    0