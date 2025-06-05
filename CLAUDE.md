# PaylKoyn - Decentralized File Storage on Cardano

## Project Overview

PaylKoyn is a **decentralized file storage system** built on the Cardano blockchain that implements the **AdaFS (Ada File System) 1.0 protocol**. The system enables users to store arbitrary files directly on the Cardano blockchain using transaction metadata, generate procedural NFTs with randomized traits, and retrieve files through cryptographically-linked transaction chains.

### Core Functionality
- **Blockchain File Storage**: Store files as raw bytes in Cardano transaction metadata (label `6673`)
- **Procedural NFT Generation**: Create randomized NFTs with trait-based image composition
- **Integrated Payment Processing**: Cardano wallet integration with automated fee estimation
- **File Retrieval**: Reconstruct files from blockchain transaction chains

## Architecture & Services

### Service Architecture (6 main components)

#### 1. PaylKoyn.Web (Frontend)
- **Technology**: Blazor Server with MudBlazor components
- **Features**: File upload interface, NFT gallery, Cardano wallet integration
- **Assets**: TypeScript + Bun.js build pipeline, TailwindCSS styling
- **Real-time**: SignalR for large file transfers (32MB buffer)

#### 2. PaylKoyn.API (Data Layer)
- **Technology**: FastEndpoints with Scalar API documentation
- **Purpose**: Cardano blockchain data queries (UTXOs, protocol parameters, scripts)
- **Database**: PostgreSQL via Entity Framework Core
- **Deployment**: Railway with IPv6 networking

#### 3. PaylKoyn.Node (File Orchestration)
- **Technology**: .NET 9.0 with background workers
- **Purpose**: Upload request handling, wallet generation, file caching
- **Features**: Fee estimation, payment processing, file retrieval
- **Storage**: SQLite for local wallet management

#### 4. PaylKoyn.Sync (Blockchain Indexing)
- **Technology**: Argus.Sync framework with custom reducers
- **Purpose**: Real-time Cardano blockchain synchronization
- **Data**: Transaction/output tracking in PostgreSQL
- **Connection**: Unix socket bridge to cardano-node via socat

#### 5. PaylKoyn.ImageGen (NFT Generation)
- **Technology**: ImageSharp for image composition
- **Purpose**: Procedural NFT creation with trait randomization
- **Assets**: Layered image system (background, body, clothing, eyes, hat)
- **Traits**: Weighted randomization with group-based selection

#### 6. PaylKoyn.Data (Shared Models)
- **Technology**: Entity Framework Core with PostgreSQL
- **Purpose**: Shared data models, migrations, and services
- **Models**: OutputBySlot, TransactionBySlot, TransactionSubmission, Wallet

## Database Schema

### Key Entities
```sql
-- UTXO tracking with address and script data
OutputsBySlot (OutRef, Address, Lovelace, Assets, ScriptHash, SpentSlot, SpentTxHash)

-- Transaction metadata and body storage  
TransactionsBySlot (Hash, Slot, Index, Body, Metadata)

-- Transaction submission status tracking
TransactionSubmissions (Hash, TxRaw, Status, DateSubmitted, ConfirmedSlot)

-- Local wallet management
Wallets (Address, PrivateKey, PublicKey, StakeAddress)
```

### Migration Management
- **Current**: `20250604200830_AddTransactionSubmissions` 
- **Tools**: EF Core with Railway PostgreSQL
- **Connection**: `"Host=mainline.proxy.rlwy.net;Database=railway;Username=postgres;Password=${PGPASSWORD}"`

## Deployment Infrastructure

### Docker Configuration
All services use multi-stage builds with .NET 9.0:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Runtime stage  
FROM mcr.microsoft.com/dotnet/aspnet:9.0
```

### Railway Deployment
- **Networking**: IPv6 compatibility with socat TCP bridges
- **Storage**: Persistent volumes for blockchain data (`/data`)
- **Database**: PostgreSQL with cross-service environment variables
- **CI/CD**: GitHub Actions → GHCR → Railway

### Cardano Infrastructure
- **Node**: Preview testnet with Mithril fast sync
- **Connection**: `cardano-node:3333` → Unix socket `/ipc/node.socket`
- **Protocol**: Ouroboros consensus via socat bridge

## API Endpoints

### Core REST API
```
GET /api/v1/addresses/{address}/utxos - Query UTXOs by address
GET /api/v1/epochs/protocol-parameters - Get network parameters  
GET /api/v1/scripts/{scriptHash} - Get script by hash
POST /api/v1/upload/request - Generate upload wallet
POST /api/v1/upload/receive - Process file upload
POST /api/v1/generate/nft - Create procedural NFT
```

### Environment Variables (Railway)
```bash
# Database
ConnectionStrings__DefaultConnection="Data Source=/data/wallet.db"

# Cardano Configuration
CardanoNodeConnection__NetworkMagic=2
CardanoNodeConnection__SocketPath="/ipc/node.socket"
BlockfrostApiKey="previewBVVptlCv4DAR04h3XADZnrUdNTiJyHaJ"
RewardAddress="addr_test1qp0wm2cqf3z5qejaeg75g422675ujzfwdxcmqkq83qgj9hmmmsx4jmdrl442mkv2gqh4qecsaws3cw0farcdfh5hehqq5j6wx5"

# File Storage
File__ExpirationMinutes=5
File__TempFilePath="/tmp"
File__RevenueFee=3000000

# NFT Generation
NftBaseName="PaylKoyn"
GroupWeight__Base=10
GroupWeight__Pyro=5
Minting__MintingFee=100000000
```

## Workflows

### File Upload Flow
1. **Request**: User initiates upload → generates temporary wallet
2. **Payment**: Fee estimation → user sends ADA to wallet address  
3. **Processing**: Background worker detects payment → encodes file as AdaFS
4. **Storage**: File submitted as transaction metadata to Cardano blockchain

### NFT Generation Flow
1. **Creation**: User triggers generation → random trait selection
2. **Composition**: ImageSharp layers assets (background → body → clothing → eyes → hat)
3. **Minting**: Payment processing → NFT transaction with CIP-25 metadata
4. **Completion**: NFT minted on Cardano with AdaFS image storage

## Technical Stack

### Core Technologies
- **.NET 9.0**: Latest C# with minimal APIs and top-level programs
- **Chrysalis**: Cardano CBOR serialization and blockchain interaction
- **Argus.Sync**: Cardano blockchain synchronization framework
- **FastEndpoints**: High-performance API framework with OpenAPI

### Frontend Technologies  
- **Blazor Server**: Real-time C# web applications
- **MudBlazor**: Material Design component library
- **Bun.js**: Fast JavaScript bundler and package manager
- **TailwindCSS**: Utility-first CSS framework

### Cardano Integration
- **Network**: Preview testnet for development/testing
- **Protocol**: AdaFS 1.0 for file storage (metadata label `6673`)
- **Standards**: CIP-25 for NFT metadata, CBOR encoding
- **Connection**: Direct Ouroboros protocol via Unix sockets

## Recent Fixes & Improvements

### Socket Management (Critical)
- **Issue**: Unix socket creation failures ("Cannot assign requested address")
- **Solution**: Proper permissions, socket cleanup on restart, IPv6 compatibility
- **Files**: `deployments/paylkoyn-sync/Dockerfile`

### Container Dependencies
- **Issue**: ImageSharp hanging in production (missing native libraries)  
- **Solution**: Copy Assets folder to container, remove unnecessary native deps
- **Files**: `deployments/paylkoyn-imagegen/Dockerfile`

### Database Migrations
- **Recent**: `AddTransactionSubmissions` for transaction status tracking
- **Command**: `dotnet ef database update --connection "Host=mainline.proxy.rlwy.net..."`

### Build Pipeline
- **Issue**: Bun installation failures in Docker
- **Solution**: Add `unzip` dependency for Bun package extraction
- **Files**: Web service Dockerfile

## Common Issues & Solutions

### Deployment Issues
1. **Socket Permissions**: Ensure `/ipc` directory has `app:app` ownership
2. **Asset Loading**: Copy `src/PaylKoyn.ImageGen/Assets` to container
3. **IPv6 Networking**: Use `TCP6-LISTEN` with `bind=[::]` for Railway
4. **Volume Permissions**: Set `/data` directory permissions at startup

### Development Issues  
1. **EF Migrations**: Run from PaylKoyn.Sync context targeting PaylKoyn.Data
2. **Socket Cleanup**: Remove existing socket files on socat restart
3. **CORS**: Configure for dApp wallet connections
4. **Memory**: Use DbContextFactory for connection pooling

## Domain Configuration

### Production URLs
- **Root**: `paylkoyn.io` (Cloudflare → redirect to www)
- **Web**: `www.paylkoyn.io` → Railway (`edj67g5l.up.railway.app`)
- **ImageGen**: `imagegen.paylkoyn.io` → Railway (`e5qig7xq.up.railway.app`)

### Development
- **API**: `localhost:5000` (PaylKoyn.API)
- **Web**: `localhost:5001` (PaylKoyn.Web)  
- **ImageGen**: `localhost:5246` (PaylKoyn.ImageGen)

## Testing & Verification

### API Testing
```bash
# Test NFT mint request
curl -k https://imagegen.paylkoyn.io/api/v1/mint/request \
  -X POST -H 'Content-Type: application/json' \
  -d '{"userAddress": "addr_test1qp..."}'

# Check mint status  
curl -k https://imagegen.paylkoyn.io/api/v1/mint/requests/{userAddress}
```

### Service Health
- **Sync**: Check for "Cannot assign requested address" errors
- **ImageGen**: Verify Assets folder exists, no hanging on image generation
- **Web**: Test file upload with wallet connection
- **API**: Validate UTXO queries and protocol parameters

## Project Strengths

1. **Pure Blockchain Storage**: No off-chain dependencies for file persistence
2. **Modular Architecture**: Loosely coupled services with clear responsibilities
3. **Production Ready**: Comprehensive Docker deployment with CI/CD
4. **Type Safety**: Full-stack C# with strong typing throughout  
5. **Standards Compliance**: Proper Cardano CBOR and CIP specifications
6. **Real-time Sync**: Live blockchain indexing with custom reducers

This system represents a sophisticated implementation of decentralized file storage that leverages Cardano's transaction metadata capabilities while maintaining enterprise-grade architecture patterns and deployment automation.