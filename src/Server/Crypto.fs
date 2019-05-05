module Blockchain.Crypto

open System
open System.Security.Cryptography
open System.Text

type Signature = 
    | Signature of string
    member x.Value = match x with | Signature s -> s

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

let exportPublicKey (rsa : RSA) =
    let p = rsa.ExportParameters true
    let param = [
        p.Modulus
        p.Exponent
        p.Exponent
        p.Exponent
        p.Exponent
        p.Exponent
        p.Exponent
        p.Exponent
    ]
    List.map Convert.ToBase64String param

let importKey (comp : string array) =
    let rsa = RSA.Create()
    let key = Array.map Convert.FromBase64String comp
    let p = 
        RSAParameters (
            Modulus = key.[0],
            Exponent = key.[1], 
            D = key.[2], 
            P = key.[3], 
            Q = key.[4], 
            DP = key.[5], 
            DQ = key.[6], 
            InverseQ = key.[7] 
        )
    rsa.ImportParameters p
    rsa

let savePrivateKey rsa path = 
    let key = exportPrivateKey rsa 
    IO.File.WriteAllLines (path, key)

let savePublicKey rsa path = 
    let key = exportPublicKey rsa 
    IO.File.WriteAllLines (path, key)

let loadKey path = 
    let key = IO.File.ReadAllLines path 
    importKey key
    
let sha256 = System.Security.Cryptography.SHA256.Create()

let sha256HashInt (n : int) =
        sha256.ComputeHash (BitConverter.GetBytes n) 
        |> BitConverter.ToString 
        |> fun x -> x.Replace ("-", "")

let sha256HashStr (msg : string) =
        sha256.ComputeHash (Encoding.UTF8.GetBytes msg)
        |> BitConverter.ToString 
        |> fun x -> x.Replace ("-", "")

let verifyPoW x = sha256HashInt x |> fun p1 -> p1.EndsWith "00000" 

let signString (rsa : RSA) (str : string) =
    let b = Text.Encoding.UTF8.GetBytes str
    rsa.SignData (b, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    |> Convert.ToBase64String 
    |> Signature

let verifySignature (rsa : RSA) (sign : Signature) (str : string) =
    let b = Text.Encoding.UTF8.GetBytes str
    let s = Text.Encoding.UTF8.GetBytes sign.Value
    rsa.VerifyData (b, s, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)

let rec proofOfWork p0 x =
    if verifyPoW (p0 + x) then
        x
    else
        proofOfWork p0 (x + 1)
    