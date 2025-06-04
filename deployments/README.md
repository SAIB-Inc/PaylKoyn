# PaylKoyn Deployments

This directory contains Railway deployment configurations for PaylKoyn infrastructure services.

## Overview

The deployment consists of Cardano blockchain infrastructure services that provide a foundation for PaylKoyn's decentralized file storage system.

## Services

### cardano-node
**Purpose**: Cardano blockchain node with TCP socket forwarding for Railway networking

**Configuration**:
- Base image: `ghcr.io/blinklabs-io/cardano-node:10.4.1-3`
- Network: Preview testnet
- Mithril snapshot: Enabled for fast initial sync
- TCP bridge: Unix socket → port 3333 via socat

**Key features**:
- IPv6 socat bridge: `TCP6-LISTEN:3333,bind=[::]` 
- Automatic socket detection at `/ipc/node.socket`
- Bridge monitoring with auto-restart capability
- Persistent volume mounting at `/data/db`

**Exposed ports**:
- 3001: Cardano node P2P networking
- 3333: TCP bridge for inter-service communication

### cardano-cli-test
**Purpose**: Test container that validates Cardano node connectivity and functionality

**Configuration**:
- Base image: Alpine Linux with cardano-cli 10.4.1
- Creates reverse TCP→Unix socket bridge
- Continuous blockchain tip querying for monitoring

**Test capabilities**:
- DNS resolution and IPv6 connectivity testing
- Port availability scanning
- Socket bridge health monitoring
- Blockchain query validation

## Network Architecture

```
Railway Internal Network (IPv6)
│
├── cardano-node:3001 (P2P networking)
├── cardano-node:3333 (TCP bridge)
│   └── socat TCP6-LISTEN:3333 ← /ipc/node.socket
│
└── cardano-cli-test
    └── socat UNIX-LISTEN:/tmp/node.socket ← TCP:cardano-node:3333
        └── cardano-cli --socket-path /tmp/node.socket
```

## Deployment Steps

1. **Deploy cardano-node**:
   ```bash
   cd cardano-node
   railway up --environment preview --service cardano-node
   ```

2. **Wait for sync**: Monitor logs for socket creation and bridge startup:
   ```
   Socket found! Starting socat TCP forwarder on port 3333...
   SOCAT STATUS CHECK - port 3333 listener active
   ```

3. **Deploy cardano-cli-test**:
   ```bash
   cd cardano-cli-test
   railway up --environment preview --service cardano-cli-test
   ```

4. **Verify functionality**: Check logs for successful blockchain queries:
   ```json
   {
     "block": 3298314,
     "epoch": 952,
     "era": "Conway", 
     "syncProgress": "100.00"
   }
   ```

## Key Technical Solutions

### IPv6 Networking
Railway uses IPv6 for internal service communication. The socat bridge uses `TCP6-LISTEN` with `bind=[::]` to ensure compatibility.

### Unix Socket Forwarding
Cardano node creates Unix sockets that aren't accessible between Railway containers. The socat TCP bridge solves this limitation.

### Automatic Recovery
Both services include monitoring and auto-restart capabilities for robust operation.

## Monitoring

**cardano-node status indicators**:
- `=== PAYL KOYN WRAPPER SCRIPT STARTING ===`
- `Socket found! Starting socat TCP forwarder on port 3333...`
- `SOCAT STATUS CHECK - port 3333 listener active` (every 30s)

**cardano-cli-test status indicators**:
- Successful DNS resolution to IPv6 address
- Port 3333 connectivity established
- `Query successful!` with blockchain tip data

## Troubleshooting

**Port 3333 connection refused**:
- Check cardano-node logs for socket detection
- Verify socat bridge startup messages
- Ensure IPv6 binding is active

**Socket not found**:
- Wait for cardano-node full startup
- Check `/ipc/node.socket` creation in logs
- Verify Mithril snapshot download completion

**IPv6 connectivity issues**:
- Use `nslookup cardano-node` to get IPv6 address
- Test direct IP connectivity with `ping`
- Verify Railway internal networking

## Next Steps

With this infrastructure operational, you can now deploy:
- PaylKoyn.Sync service for blockchain synchronization
- PaylKoyn.Node for file storage operations
- Other Cardano-dependent services

All services can connect to the Cardano node via `cardano-node:3333` TCP endpoint.