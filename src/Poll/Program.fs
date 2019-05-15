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
    | All
    | Key of key : string
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | All  -> "Run all tests."
                | Key _ -> "Use public key pair 'key'."
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


let cryptoExamples keyfile =
    let priv = loadKey keyfile
    let pub = loadPubKey (keyfile + ".pub")

    let s1 = signString priv "test1"
    let v1 = verifySignature priv s1 "test1"
    let v2 = verifySignature priv s1 "test2"
    printfn "sig 1: %A " (v1, v2)

    // encrypt with private key
    let e1 = encryptString priv "test1"
    let d1 = decryptString priv e1
    let d2 = decryptString pub e1
    printfn "dec 1: %A " (d1, d2)

    // encrypt with public key
    let e2 = encryptString pub "test2"
    let d1' = decryptString priv e2
    let d2' = decryptString pub e2
    printfn "dec 2: %A " (d1', d2')

    let p n = proofOfWork n 1
    let x0 = 101
    let x1 = x0 + p x0
    let x2 = x1 + p x1
    let x3 = x2 + p x2
    let ps = [x1; x2; x3]
    printfn "pow: %A" (List.zip ps (List.map verifyPoW ps))

let testsAndExamples (args : ParseResults<TestArgs>) =
    mqttTests ()
    let keyfile = args.GetResult Key
    cryptoExamples keyfile

let keygen (args : ParseResults<PubkeyArgs>) =
    let rsa = RSA.Create()
    match args.TryGetResult Generate with
    | Some path ->
        savePrivateKey rsa path
        savePublicKey rsa (path + ".pub")
        printfn "Geneated RSA keys: %s" path
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
