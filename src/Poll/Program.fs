module Blockchain.Poll

open System
open System.Security.Cryptography
open Argu
open Blockchain.Mqtt
open Blockchain.Crypto

type VoteArgs =
    | File of path : string
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | File _ -> "JSON file with vote specification."
type TestArgs =
    | Mqtt 
    | Crypto
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Mqtt  -> "Send test messages to the MQTT broker."
                | Crypto  -> "Test cryptography functions."
type PubkeyArgs =
    | Generate of path : string
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Generate _ -> "Generate key paris."
type CLIArguments =
    | Version
    | [<AltCommandLine("-v")>] Verbose
    | [<CliPrefix(CliPrefix.None)>] Vote of ParseResults<VoteArgs>
    | [<CliPrefix(CliPrefix.None)>] Pubkey of ParseResults<PubkeyArgs>
    | [<CliPrefix(CliPrefix.None)>] Test of ParseResults<TestArgs>
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Version -> "Prints the program version."
                | Verbose -> "Print a lot of output to stdout."
                | Vote _ -> "Place a vote in an election or poll."
                | Pubkey _ -> "Generate keys for public key cryptography."
                | Test _ -> "Tests and examples."

let mqttTests () =
    let client = mqttConnect "localhost"
    mqttSendTransaction client "new tx"
    mqttSendBlock client "new block"
    mqttSay client "hi there"
    mqttDisconnect client

let cryptoExamples () =
    let rsa = RSA.Create()
    let s = signString rsa "foo"
    let v = verifySignature rsa s "foo" 
    let v' = verifySignature rsa s "fooo"
    printfn "%A" (v, v')

    let proof = proofOfWork 12345 0
    let sha = sha256HashInt (12345 + proof)
    printfn "PoW = %A %A" proof sha

    let priv = loadKey "identity"
    
    let s1 = signString priv "test1"
    let v1 = verifySignature priv s1 "test1"
    let v2 = verifySignature priv s1 "test2"
    printfn "sig 1: %A %A" v1 v2

let testsAndExamples (args : ParseResults<TestArgs>) =
    mqttTests ()
    cryptoExamples ()

let keygen (args : ParseResults<PubkeyArgs>) =
    let rsa = RSA.Create()
    match args.TryGetResult Generate with
    | Some path -> 
        savePrivateKey rsa path
        savePublicKey rsa (path + ".pub")
        printfn "Geneated RSA keys in %s" path
    | None -> ()

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<CLIArguments>(programName = "Poll")
    try
        let results = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true) 
        let isVote = results.TryGetResult Vote 
        let isPubkey = results.TryGetResult Pubkey 
        let isTest = results.TryGetResult Test 
        if isVote.IsSome then
            ()
        else if isPubkey.IsSome then
           keygen isPubkey.Value 
        else if isTest.IsSome then
            testsAndExamples isTest.Value
    with e ->
        printfn "%s" e.Message
    0 // return an integer exit code
