#!/bin/bash
set -euo pipefail

# ===== CONFIGURATION =====
readonly SCRIPT_NAME="paylkoyn-sync-startup"
readonly CARDANO_SOCKET_PATH="${CARDANO_SOCKET_PATH:-/ipc/node.socket}"
readonly PAYLKOYN_SYNC_DIR="${PAYLKOYN_SYNC_DIR:-/app/paylkoyn-sync}"
readonly DATA_DIR="/data"

# Timeouts (in seconds)
readonly CHUNK_VALIDATION_TIMEOUT_AFTER_COMPLETE=600  # 10 minutes after validation
readonly SOCKET_READY_TIMEOUT=60                      # 1 minute after socket exists
readonly PROCESS_CHECK_INTERVAL=5                     # Check processes every 5 seconds
readonly STATUS_REPORT_INTERVAL=30                    # Report status every 30 seconds

# ===== FUNCTIONS =====

# Logging functions
log_info() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] [INFO] $*"
}

log_error() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] [ERROR] $*" >&2
}

handle_error() {
    log_error "$1"
    exit 1
}

# Function to capture cardano-node output from various sources
capture_node_output() {
    local lines="${1:-20}"
    local output=""
    
    # Try journalctl first
    if command -v journalctl >/dev/null 2>&1; then
        output=$(journalctl -u cardano-node --since "10 seconds ago" 2>/dev/null || true)
    fi
    
    # Try docker logs if journalctl didn't work
    if [ -z "$output" ] && command -v docker >/dev/null 2>&1; then
        output=$(docker logs cardano-node --tail "$lines" 2>/dev/null || true)
    fi
    
    # Try process output as last resort
    if [ -z "$output" ] && [ -n "${CARDANO_PID:-}" ]; then
        output=$(tail -n "$lines" "/proc/$CARDANO_PID/fd/1" 2>/dev/null || true)
    fi
    
    echo "$output"
}

# Check if process is running
is_process_running() {
    local pid="$1"
    kill -0 "$pid" 2>/dev/null
}

# Setup environment
setup_environment() {
    log_info "=== UNIFIED PAYLKOYN-SYNC CONTAINER STARTING ==="
    
    # Ensure data directory has correct permissions
    if [ -d "$DATA_DIR" ]; then
        log_info "Setting permissions on $DATA_DIR directory..."
        chown -R "$(id -u):$(id -g)" "$DATA_DIR" 2>/dev/null || true
    fi
    
    # Display configuration
    log_info "Configuration:"
    log_info "  Network: ${NETWORK:-preview}"
    log_info "  Restore snapshot: ${RESTORE_SNAPSHOT:-true}"
    log_info "  Socket path: $CARDANO_SOCKET_PATH"
    log_info "  PaylKoyn sync dir: $PAYLKOYN_SYNC_DIR"
}

# Start cardano-node
start_cardano_node() {
    log_info "Starting cardano-node with Blink Labs entrypoint..."
    
    /usr/local/bin/entrypoint "$@" &
    CARDANO_PID=$!
    
    log_info "Cardano-node started with PID: $CARDANO_PID"
}

# Wait for cardano-node to be ready
wait_for_cardano_ready() {
    local total_wait=0
    local socket_timeout_started=false
    local socket_timeout_wait=0
    local last_progress=""
    
    log_info "Waiting for cardano-node to be ready..."
    
    while true; do
        # Check if cardano-node is still running
        if ! is_process_running "$CARDANO_PID"; then
            handle_error "cardano-node process died during initialization"
        fi
        
        total_wait=$((total_wait + 1))
        
        # Capture node output once
        local node_output
        node_output=$(capture_node_output 50)
        
        # Check various states
        if [ ! -S "$CARDANO_SOCKET_PATH" ]; then
            # Socket doesn't exist yet - check for chunk validation
            if [ "$socket_timeout_started" = false ]; then
                # Look for chunk validation progress
                if echo "$node_output" | grep -q "Validating chunk\|Validated chunk"; then
                    local progress
                    progress=$(echo "$node_output" | grep -oE "Progress: [0-9]+\.[0-9]+%" | tail -1 | grep -oE "[0-9]+\.[0-9]+" || true)
                    if [ -n "$progress" ] && [ "$progress" != "$last_progress" ]; then
                        log_info "Chunk validation progress: ${progress}%"
                        last_progress="$progress"
                        
                        # Check if validation is complete (using integer comparison)
                        local progress_int
                        progress_int=$(echo "$progress" | cut -d. -f1)
                        if [ "$progress_int" -ge 99 ]; then
                            log_info "Chunk validation complete! Starting timeout..."
                            socket_timeout_started=true
                        fi
                    fi
                elif echo "$node_output" | grep -q "Chain extended, new tip"; then
                    log_info "Chain sync detected - starting timeout..."
                    socket_timeout_started=true
                fi
            else
                # Timeout has started
                socket_timeout_wait=$((socket_timeout_wait + 1))
                if [ $socket_timeout_wait -ge $CHUNK_VALIDATION_TIMEOUT_AFTER_COMPLETE ]; then
                    handle_error "Timeout waiting for socket after chunk validation ($CHUNK_VALIDATION_TIMEOUT_AFTER_COMPLETE seconds)"
                fi
            fi
        else
            # Socket exists - check if it's ready to accept connections
            if echo "$node_output" | grep -qE "(LocalSocketUp|TrServerStarted).*${CARDANO_SOCKET_PATH}"; then
                log_info "Cardano-node socket is ready and accepting connections!"
                return 0
            fi
            
            # Socket exists but not ready yet
            local socket_wait=$((total_wait - socket_timeout_wait))
            if [ $socket_wait -ge $SOCKET_READY_TIMEOUT ]; then
                handle_error "Socket exists but not accepting connections after $SOCKET_READY_TIMEOUT seconds"
            fi
        fi
        
        # Status updates
        if [ $((total_wait % STATUS_REPORT_INTERVAL)) -eq 0 ] && [ $total_wait -gt 0 ]; then
            local minutes=$((total_wait / 60))
            local seconds=$((total_wait % 60))
            log_info "Still waiting... (${minutes}m ${seconds}s elapsed)"
            
            if [ ! -S "$CARDANO_SOCKET_PATH" ]; then
                if [ "$socket_timeout_started" = false ]; then
                    log_info "  Waiting for chunk validation to complete (no timeout until 100%)"
                else
                    local remaining=$((CHUNK_VALIDATION_TIMEOUT_AFTER_COMPLETE - socket_timeout_wait))
                    log_info "  Chunk validation complete, waiting for socket (timeout in ${remaining}s)"
                fi
            else
                log_info "  Socket exists, waiting for it to accept connections..."
            fi
        fi
        
        sleep 1
    done
}

# Validate PaylKoyn.Sync environment
validate_sync_environment() {
    log_info "Validating PaylKoyn.Sync environment..."
    
    # Check directory exists
    if [ ! -d "$PAYLKOYN_SYNC_DIR" ]; then
        handle_error "PaylKoyn.Sync directory not found at $PAYLKOYN_SYNC_DIR"
    fi
    
    # Check executable exists
    if [ ! -f "$PAYLKOYN_SYNC_DIR/PaylKoyn.Sync.dll" ]; then
        handle_error "PaylKoyn.Sync.dll not found in $PAYLKOYN_SYNC_DIR"
    fi
    
    # Verify socket is accessible
    if [ ! -S "$CARDANO_SOCKET_PATH" ]; then
        handle_error "Socket not found at $CARDANO_SOCKET_PATH after validation"
    fi
    
    # Log environment variables
    log_info "Environment variables:"
    log_info "  ASPNETCORE_ENVIRONMENT=Railway"
    log_info "  CardanoNodeConnection__UnixSocket__Path=$CARDANO_SOCKET_PATH"
    log_info "  ConnectionStrings__DefaultConnection=${ConnectionStrings__DefaultConnection:-[not set]}"
}

# Start PaylKoyn.Sync
start_paylkoyn_sync() {
    log_info "Starting PaylKoyn.Sync..."
    
    cd "$PAYLKOYN_SYNC_DIR" || handle_error "Failed to change to PaylKoyn.Sync directory"
    
    # Set environment
    export ASPNETCORE_ENVIRONMENT=Railway
    export CardanoNodeConnection__UnixSocket__Path="$CARDANO_SOCKET_PATH"
    
    # Start with output capture
    dotnet PaylKoyn.Sync.dll 2>&1 | sed 's/^/[SYNC] /' &
    SYNC_PID=$!
    
    # Give it a moment to start and verify it's running
    sleep 2
    if ! is_process_running "$SYNC_PID"; then
        handle_error "PaylKoyn.Sync failed to start or crashed immediately"
    fi
    
    log_info "PaylKoyn.Sync started successfully with PID: $SYNC_PID"
}

# Cleanup function
cleanup() {
    log_info "Shutting down..."
    
    # Stop PaylKoyn.Sync
    if [ -n "${SYNC_PID:-}" ] && is_process_running "$SYNC_PID"; then
        log_info "Stopping PaylKoyn.Sync (PID: $SYNC_PID)..."
        kill "$SYNC_PID" 2>/dev/null || true
        
        # Wait up to 10 seconds for graceful shutdown
        local wait_count=0
        while is_process_running "$SYNC_PID" && [ $wait_count -lt 10 ]; do
            sleep 1
            wait_count=$((wait_count + 1))
        done
        
        # Force kill if still running
        if is_process_running "$SYNC_PID"; then
            log_info "Force killing PaylKoyn.Sync..."
            kill -9 "$SYNC_PID" 2>/dev/null || true
        fi
    fi
    
    # Stop cardano-node
    if [ -n "${CARDANO_PID:-}" ] && is_process_running "$CARDANO_PID"; then
        log_info "Stopping cardano-node (PID: $CARDANO_PID)..."
        kill "$CARDANO_PID" 2>/dev/null || true
    fi
    
    # Wait for all background processes
    wait
    log_info "Shutdown complete"
}

# Monitor processes
monitor_processes() {
    log_info "Monitoring both processes..."
    
    local last_status_time=$SECONDS
    
    while true; do
        # Check cardano-node
        if ! is_process_running "$CARDANO_PID"; then
            log_error "cardano-node process died unexpectedly"
            return 1
        fi
        
        # Check PaylKoyn.Sync
        if ! is_process_running "$SYNC_PID"; then
            log_error "PaylKoyn.Sync process died unexpectedly"
            return 1
        fi
        
        # Status report
        local current_time=$SECONDS
        if [ $((current_time - last_status_time)) -ge $STATUS_REPORT_INTERVAL ]; then
            log_info "Status: Both processes running (cardano-node: $CARDANO_PID, sync: $SYNC_PID)"
            last_status_time=$current_time
        fi
        
        sleep $PROCESS_CHECK_INTERVAL
    done
}

# ===== MAIN EXECUTION =====

# Set up signal handlers early
trap cleanup EXIT SIGTERM SIGINT

# Initialize
setup_environment

# Start cardano-node
start_cardano_node

# Wait for cardano-node to be ready
wait_for_cardano_ready

# Validate sync environment
validate_sync_environment

# Start PaylKoyn.Sync
start_paylkoyn_sync

# Monitor both processes
monitor_processes

# If monitoring returns, it means a process died
exit 1