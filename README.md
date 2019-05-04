# Exam: INF-3910 Blockchain

Blockchains are pretty mind bending, but surprisingly simple at the core.
Your task is to develop a simple, distributed blockchain for registering
elections and public voting. The system will use RSA public key cryptography
for digital signing and verification, and the MQTT pub-sub protocol for
communication between nodes.

You will be given a stub project, with plenty of helper functions to get you quickly started.

You should read reference 1 before continuing. Reference 2 contains a very
detailed description of the Bitcoin blockchain, and is a great resource for
more advanced details which are beyond this exercise. I also assume familarity with cryptographic hash functions and public key cryptography.

## Theory

### Blocks and the block chain

A blockchain is just a singly linked list, where every element of the list contains:

1. An index (serial number)
2. A reference to the crypyographic hash of the previous element in the list
3. The proof of work
4. A timestamp
5. A payload (list of transactions)

This makes every element in the list dependent on the complete history of the
whole list! If so much as one bit is modified anywhere in the list, all
subsequent hashes will fail to verify [^1]. 

Every element in the list is called a block. The payload in a block is called
a transaction. Usually transactions need to be verified somehow, before they
are accepted into the chain (e.g. user X has only voted once in election E).

```json
[
    {
      "index": 1,
      "previous_hash": 1,
      "proof": 100,
      "timestamp": 1506280650.770839,
      "transactions": []
    },
    {
      "index": 2,
      "previous_hash": "c099bc...bfb7",
      "proof": 35293,
      "timestamp": 1506280664.717925,
      "transactions": [ { ... } ]
    },
    {
      "index": 3,
      "previous_hash": "eff91a...10f2",
      "proof": 35089,
      "timestamp": 1506280666.1086972,
      "transactions": [ { ... } ]
    }
  ]
```

[^1]: This is also how git maintains it's commit logs.

### Proof of Work (PoW)

Proof of work is a cryptographic puzzle which can only be solved by brute force. The purpose of PoW is twofold:

1. Enforce a variable dealy, proportional to the difficulty of the challenge, to avoid having many nodes broadcast new blocks (nearly) simultaneously.
2. Make it very, very expensive to insert fraudulent blocks. The first node to solve the PoW gets its block inserted into the chain. In order to insert a malicious block, the fraudster would have to overcome the combined compute power of all the other nodes combined.

The PoW challenge is difficult to compute, and easy to verify. The algorithm is quite simple:

1. Every block contains the proof p0 (an integer) of the previous block
2. The new proof is constructed by finding an integer x such that the the SHA256 of p0 + x ends in e.g. four zeros:

```fsharp
let sha256 = System.Security.Cryptography.SHA256.Create()
let hash (n : int) =
        sha256.ComputeHash (BitConverter.GetBytes n) 
        |> BitConverter.ToString 
        |> fun x -> x.Replace ("-", "")

let verify x = hash x |> fun p1 -> p1.EndsWith "0000" 

let rec proofOfWork p0 x =
    if verify (p0 + x) then
        x
    else
        proofOfWork p0 (x + 1)
```

The expense of the PoW puzzle guarantees that nobody can inject "fake" or
fraudulent blocks into the chain.

### Consensus

Consensus is everybody agreeing on a single truth, in this case a single
canonical chain of blocks. The cleverness of blockchains is how consesus can
be reached, without giving anybody any kind of special trust.

In a PoW-based blockchain all participating nodes are working as hard as they
can to produce new blocks, and broadcast them to the other nodes. The
cryptographic hashes ensure that they form an unbroken and correctly ordered
chain. The block that wins and becomes the accepted truth is the block that
first reaches enough nodes with enough computing power to outcompute the rest
of nodes. Thus it may happen that multiple nodes complete the PoW before
receiving a new block, and broadcast their blocks. Now a number of nodes have
added valid but "wrong" blocks to their chain, and are thus working on a
different chain than the majority.

This is where the serial numbers come to rescue. Assuming that the majority
of nodes hold the majority of compute power combined, they will be producing
new blocks faster than the minorities. If a node receives a block with a
larger serial number than the current number and which can't be verified
because it's parent is "missing", it implies that the node is working on the
"wrong" chain (fork or side-chain), for which there is no consenus. The node
must then ask the network for the correct, missing blocks by back stepping
until consistency is reached again.

### Workflow

A blockchain network consists of a (variable) number of particiapting nodes (miners). At a minimum three processing nodes are required in order to reach
consensus. Clients can connect to any of the nodes and post new transactions
to be added to the blockchain.

Upon receiving a transaction, a processing node:

1. Broadcasts it to all participating nodes
2. Puts the transaction in the transaction queue
3. Verifies the transaction, and discards invalid transactions.
4. Adds (some) transaction(s) to the new block being forged
5. Starts working on the proof-of-work challenge [^2]

Two things can happen next:

#### The node completes the PoW

If the node completes the PoW without receiving a new block from the network,
it creates a new block with the provided proof and broadcasts it to all
participating nodes. The node then preceeds to process the next block.

#### The node receives the next block

If a node receivces a block before completing it's own PoW, the node verifies
it's validity by checking the PoW and serial number. If the block is valid, the node:

1. Stops working on it's own block
2. Adds the block to the local blockchain.
3. Prunes the transaction queue
4. Starts working on the next block

Typically every block worked on by different nodes will be unique, because
nodes can aggregate transactions and add their own information to them (e.g.
transaction fees, etc.).

[^2]: Note: Normally the node starts working on the PoW as soon as it has
  created or received the next block in the chain. On low traffic chains,
  where new transactions arrive slower than the PoW chanllenge, this defeats
  the PoW concept.

#### Transaction queue

The transaction queue holds all transactions which have not yet been added to a block. The first thing a node does when receiving a new transaction is to broadcast it, to ensure it's not lost if the receiving node goes down. Then it's added to the transaction que, awaiting to be added to a new block or pruned. 

When a node has completed the PoW it selects a number of transactions from
the queue, adds them to the block, and remove them from the queue. When a node receives a block, it checks the transactions and removes any matching transactions from the queue so that they are not included twice. 

### Transactions

A transaction in a blockchain can in principle be anything (i.e. a blob), but
typically blockchains enforce some set of rules for valid transactions.

In this exercise you will develop a public voting system, with the following properties:

1. Votes are public. Anyone can see who voted for what.
2. Voters can register themselves with the system
3. Anyone can create an election
4. Any registered voter can vote once, and only once, in any election

The transaction protocol ensures that:

1. Voters provide a valid public key, signed with their private key.
2. Elections are uniqe and conform to the election format
3. Votes are signed with the voters private key and are cast only once in a particular election

#### Transaction formats for the election system

Election:

```json
{
    "election": {
        "id": "unique string",
        "name": "string",
        "description": "string",
        "options": [
            {
                "name": "string",
                "description": "string"
            }
        ],
        "allowed_voters": [],
        "closes": "date and time"
    },
    "signature": "RSA signature of the election block"
}
```

Voter:

```json
{
    "voter": {
        "id": "base64 encoded sha256 has of the public key",
        "pubkey": "base64 encoded public key"
    },
    "signature": "RSA signature of the voter block"
}
```

Ballot:

```json
{
    "ballot": {
        "election": election.id,
        "choice": election.options[x],
        "voter": voter.id,
        "tx": "transaction hash of the election"
    },
    "signature": "RSA signature of the voter block"
}

```

### MQTT

MQTT is a light-weight publish-subscribe protocol, standard
in the IoT world. MQTT works by having a (number of connected) central MQTT
brokers (servers), which receive messages from clients on topics (named
chanels). The broker then distributes the messages to all clients listening to
a topic. Messaging is event driven and everything happens asynchronously.

In this exercise we will use MQTT to broadcast new transactions to the
network, and to publish new blocks. We also use MQTT to publish the node name
(GUID) and URL of a node when it connects.

## Exercises

### 1. Blockchain

1. Implement a block chain structure to add a number of unverified "blob"
   transactions to a new block. New blocks should include the proof of work and
   the hash of the previous block.
2. Create a REST API to add new transactions to the chain.
3. In the Elections project, add a command line interface to add a new transaction

### 2. Consensus

1. Implement a multi-node blockchain network over MQTT, using the provided MQTT broker
2. Implement transaction and block broadcasting over MQTT
3. Implement longest chain consensus
4. Implement chain backtracking in case of a forked chain

### 3. Voting

1. Implement the Election, Voter and Ballot transactions, each with it's own REST API
2. Implement REST APIs for getting Elections, Voters and Ballots from the blockchain
3. Implement transaction verification, ensuring that the rules of the voting process are upheld
4. Add a simple command line interface to add voters, elections and to cast votes in the Elections project. For simplicity you can use json files on disk.

### 4. Web interface

Implement a web interface:

1. Show the total number of registered voters
2. List all active and closed elections
3. Show current status of a selected election

## References

1. [Learn blockchains by building one](https://hackernoon.com/learn-blockchains-by-building-one-117428612f46)
2. [Mastering Bitcoin](https://github.com/bitcoinbook/bitcoinbook)
3. [Public key cryptography](https://en.wikipedia.org/wiki/Public-key_cryptography)
4. [Public key cryptography](https://en.wikipedia.org/wiki/Public-key_cryptography)
5. [Cryptographic hash functions](https://en.wikipedia.org/wiki/Cryptographic_hash_function)
6. [MQTT](https://en.wikipedia.org/wiki/MQTT)
7. [Publish-subscribe](https://en.wikipedia.org/wiki/Publish%E2%80%93subscribe_pattern)

## Install pre-requisites for developing

You'll need to install the following pre-requisites in order to build SAFE applications

* The [.NET Core SDK 2.2](https://www.microsoft.com/net/download)
* [FAKE 5](https://fake.build/) installed as a [global tool](https://fake.build/fake-gettingstarted.html#Install-FAKE)
* The [Yarn](https://yarnpkg.com/lang/en/docs/install/) package manager (you an also use `npm` but the usage of `yarn` is encouraged).
* [Node LTS](https://nodejs.org/en/download/) installed for the front end components.
* If you're running on OSX or Linux, you'll also need to install [Mono](https://www.mono-project.com/docs/getting-started/install/).

## Docker images

To build a Docker image from source:

```sh
docker build -t blockchain .
```

## Developing the application

To concurrently run the server and the client components in watch mode use the following command:

```bash
(mono) .paket/paket.exe install
yarn install
dotnet restore
fake build -t Run
```

## SAFE Stack Documentation

You will find more documentation about the used F# components at the following places:

* [Giraffe](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md)
* [Fable](https://fable.io/docs/)
* [Elmish](https://elmish.github.io/elmish/)
* [Fulma](https://fulma.github.io/Fulma/)

If you want to know more about the full Azure Stack and all of it's components (including Azure) visit the official [SAFE documentation](https://safe-stack.github.io/docs/).

## Troubleshooting

* **fake not found** - If you fail to execute `fake` from command line after installing it as a global tool, you might need to add it to your `PATH` manually: (e.g. `export PATH="$HOME/.dotnet/tools:$PATH"` on unix) - [related GitHub issue](https://github.com/dotnet/cli/issues/9321)
