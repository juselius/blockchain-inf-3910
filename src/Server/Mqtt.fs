module Blockchain.Mqtt

open System
open MQTTnet
open MQTTnet.Client
open MQTTnet.Client

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

let mqttSubscribe (client : IMqttClient) (topic : string) = 
    [ TopicFilter (topic, Protocol.MqttQualityOfServiceLevel.AtLeastOnce) ]
    |> client.SubscribeAsync 
    |> Async.AwaitTask 
    |> Async.Ignore
    |> Async.Start

let mqttSendTransaction client msg = 
    mqttSend client "tx" msg

let mqttSendBlock client msg = 
    mqttSend client "block" msg

let mqttSay (client : IMqttClient) msg =
    let topic = sprintf "client/%s" client.Options.ClientId
    mqttSend client topic msg

let mqttJoin (client : IMqttClient) =
    mqttSend client "join" client.Options.ClientId

let mqttSendClient (client : IMqttClient) cid msg =
    let topic = sprintf "client/%s" cid
    mqttSend client topic msg
