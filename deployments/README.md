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

### paylkoyn-sync
**Purpose**: Cardano blockchain synchronization service for PaylKoyn data indexing

**Configuration**:
- Base image: Published to GHCR via GitHub Actions
- Database: Railway Postgres with automatic schema creation
- Cardano connection: TCP bridge to cardano-node:3333 via socat

**Key features**:
- Automated Docker image publishing to `ghcr.io/saib-inc/paylkoyn/paylkoyn-sync:latest`
- EF Core migrations for database schema management
- Argus.Sync framework integration with custom reducers
- TCP→Unix socket bridge for Cardano node communication

**Exposed services**:
- Database entities: TransactionsBySlot, OutputsBySlot, ReducerStates
- Blockchain data synchronization and indexing

## Deployment Architecture

```
Railway Infrastructure (IPv6)
│
├── cardano-node:3001 (P2P networking)
├── cardano-node:3333 (TCP bridge)
│   └── socat TCP6-LISTEN:3333 ← /ipc/node.socket
│
├── cardano-cli-test (validation)
│   └── socat UNIX-LISTEN:/tmp/node.socket ← TCP:cardano-node:3333
│
├── paylkoyn-sync (blockchain indexing)
│   ├── socat UNIX-LISTEN:/tmp/preview-node.socket ← TCP:cardano-node:3333
│   └── Database connection → payl-koyn-db-1.railway.internal
│
└── payl-koyn-db-1 (PostgreSQL)
    └── Schema: TransactionsBySlot, OutputsBySlot, ReducerStates
```

## Advanced Deployment Steps

### PaylKoyn.Sync Service

1. **Database Setup**:
   - Ensure `payl-koyn-db-1` PostgreSQL service is running
   - Schema is managed via EF Core migrations

2. **Docker Image Deployment**:
   - GitHub Actions automatically builds and publishes to GHCR
   - Use image: `ghcr.io/saib-inc/paylkoyn/paylkoyn-sync:latest`
   - Package is public for Railway access

3. **Railway Configuration**:
   ```
   # Database Connection (using cross-service variables)
   ConnectionStrings__CardanoContext=Host=${{payl-koyn-db-1.PGHOST}};Database=${{payl-koyn-db-1.PGDATABASE}};Username=${{payl-koyn-db-1.PGUSER}};Password=${{payl-koyn-db-1.PGPASSWORD}};Port=${{payl-koyn-db-1.PGPORT}}
   
   # Cardano Node Configuration
   CardanoNodeConnection__ConnectionType=UnixSocket
   CardanoNodeConnection__UnixSocket__Path=/tmp/preview-node.socket
   CardanoNodeConnection__NetworkMagic=2
   CardanoNodeConnection__MaxRollbackSlots=1000
   CardanoNodeConnection__RollbackBuffer=10
   
   # Sync Configuration
   Sync__Dashboard__TuiMode=false
   Sync__Dashboard__RefreshInterval=100
   ```

4. **Database Migrations**:
   ```bash
   # Local migration to Railway database
   cd src/PaylKoyn.Sync
   dotnet ef database update --project ../PaylKoyn.Data --connection "Host=<railway-host>;Port=<port>;Database=railway;Username=postgres;Password=<password>"
   ```

5. **Verify Deployment**:
   - Check logs for successful cardano-node:3333 connection
   - Verify socket bridge creation: "Starting socat TCP→Unix bridge"
   - Confirm blockchain synchronization starts

## Technical Solutions

### Railway Environment Variables
Railway uses special syntax for cross-service variable references:
- `${{service-name.VARIABLE}}` = reference variable from another service
- `${VARIABLE}` = variable from current service

### Docker Image Publishing
Automated via GitHub Actions workflow:
- Triggers on changes to `src/PaylKoyn.Sync/**` or `deployments/paylkoyn-sync/**`
- Publishes to GitHub Container Registry (GHCR)
- Tagged with `latest` for main branch deployments

### Database Schema Management
- EF Core migrations stored in `src/PaylKoyn.Data/Migrations/`
- Schema includes Argus.Sync base entities plus custom PaylKoyn entities
- Manual migration execution preferred for production environments

### IPv6 Networking Compatibility
All socat bridges configured for IPv6 compatibility:
- cardano-node: `TCP6-LISTEN:3333,bind=[::]`
- Client services: Standard TCP connections work with IPv6

## Troubleshooting

### PaylKoyn.Sync Issues

**Database connection failed**:
- Verify Railway Postgres service is running
- Check cross-service variable syntax: `${{payl-koyn-db-1.PGHOST}}`
- Ensure service linking is configured

**Migration conflicts**:
- Run manual migration via EF CLI tools
- Check `__EFMigrationsHistory` table for applied migrations
- Use `dotnet ef database update` with direct connection string

**Cardano connection failed**:
- Verify cardano-node:3333 is accessible
- Check socat bridge logs in cardano-node service
- Ensure TCP→Unix socket bridge is operational

**Docker image not found**:
- Check GitHub Actions workflow completion
- Verify GHCR package visibility is public
- Use full image path: `ghcr.io/saib-inc/paylkoyn/paylkoyn-sync:latest`

## Next Steps

With this complete infrastructure operational, you can now:
- Deploy additional PaylKoyn services (API, Web, Node)
- Scale blockchain indexing with multiple sync instances
- Implement custom reducers for specific data requirements
- Monitor blockchain synchronization and data integrity

All services can connect to the Cardano node via `cardano-node:3333` TCP endpoint and share the centralized PostgreSQL database for data persistence.