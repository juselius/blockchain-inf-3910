#!/bin/sh

trap 'kill $(jobs -p)' EXIT

dotnet ../src/Broker/bin/Debug/netcoreapp2.2/Broker.dll &

sleep 1

SERVER_PORT=8085 dotnet ../src/Server/bin/Debug/netcoreapp2.2/Server.dll &
SERVER_PORT=8086 dotnet ../src/Server/bin/Debug/netcoreapp2.2/Server.dll &
SERVER_PORT=8087 dotnet ../src/Server/bin/Debug/netcoreapp2.2/Server.dll &

sleep 1

poll () {
    dotnet ../src/Poll/bin/Debug/netcoreapp2.2/Poll.dll $*
}

for i in `seq 1 5`; do
    poll pubkey --generate key$i
done

poll test --all --key key1
# poll election --new election1.json --key key1
# poll election --new election2.json --key key2
# poll voter --new voter1.json --key key3
# poll voter --new voter2.json --key key4
# poll voter --new voter3.json --key key5
# poll vote --cast vote1.json --voter voter1.json --key key3
# poll vote --cast vote2.json --voter voter2.json --key key4
# poll vote --cast vote3.json --voter voter3.json --key key5
# poll vote --cast vote1.json --voter voter1.json --key key4
# poll vote --cast vote1.json --voter voter2.json --key key3
