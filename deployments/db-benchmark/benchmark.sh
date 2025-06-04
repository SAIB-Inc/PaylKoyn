#!/bin/sh

echo "=== RAILWAY POSTGRES BENCHMARK STARTING ==="
echo "Database URL: ${DATABASE_URL}"
echo "Timestamp: $(date -Iseconds)"
echo

# Basic connection test
echo "--- Connection Test ---"
time psql "$DATABASE_URL" -c "SELECT version();" 2>&1
echo

# Simple query performance
echo "--- Basic Query Performance ---"
echo "Testing SELECT 1:"
time psql "$DATABASE_URL" -c "SELECT 1;" 2>&1
echo

echo "Testing NOW() function:"
time psql "$DATABASE_URL" -c "SELECT NOW();" 2>&1
echo

# Table existence check
echo "--- Table Schema Check ---"
echo "Checking existing tables:"
time psql "$DATABASE_URL" -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';" 2>&1
echo

# Test table creation and basic operations
echo "--- Table Operations Benchmark ---"
echo "Creating test table:"
time psql "$DATABASE_URL" -c "
CREATE TABLE IF NOT EXISTS benchmark_test (
    id SERIAL PRIMARY KEY,
    data TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);" 2>&1
echo

echo "Inserting test data (100 records):"
time psql "$DATABASE_URL" -c "
INSERT INTO benchmark_test (data) 
SELECT 'test_data_' || generate_series(1, 100);" 2>&1
echo

echo "Selecting from test table:"
time psql "$DATABASE_URL" -c "SELECT COUNT(*) FROM benchmark_test;" 2>&1
echo

echo "Complex query test:"
time psql "$DATABASE_URL" -c "
SELECT 
    COUNT(*) as total_records,
    MIN(created_at) as earliest,
    MAX(created_at) as latest
FROM benchmark_test;" 2>&1
echo

# Cleanup
echo "--- Cleanup ---"
echo "Dropping test table:"
time psql "$DATABASE_URL" -c "DROP TABLE IF EXISTS benchmark_test;" 2>&1
echo

echo "=== BENCHMARK COMPLETE ==="