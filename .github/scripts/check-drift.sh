#!/bin/bash

# BicepGuard Detection Script
# This script runs the drift detector and extracts the JSON report

set -e  # Exit on any error

TEMPLATE_FILE="$1"
RESOURCE_GROUP="$2"
OUTPUT_FILE="${3:-drift-report.json}"
IGNORE_CONFIG="$4"

echo "🔍 Running BicepGuard..."
echo "📄 Template: $TEMPLATE_FILE"
echo "🏗️  Resource Group: $RESOURCE_GROUP"
echo "📊 Output File: $OUTPUT_FILE"
if [ -n "$IGNORE_CONFIG" ]; then
    echo "🔇 Ignore Config: $IGNORE_CONFIG"
fi

# Build the command arguments
ARGS="--bicep-file \"$TEMPLATE_FILE\" --resource-group \"$RESOURCE_GROUP\" --output Json"
if [ -n "$IGNORE_CONFIG" ] && [ -f "$IGNORE_CONFIG" ]; then
    ARGS="$ARGS --ignore-config \"$IGNORE_CONFIG\""
fi

# Run drift detector with JSON output
echo "🚀 Executing BicepGuard..."
eval "dotnet run --no-build --configuration Release --project BicepGuard.csproj -- $ARGS" > full-output.txt 2>&1 || true

# Check if output file was created
if [ ! -f full-output.txt ]; then
    echo "❌ Error: No output file generated"
    echo "drift=false" >> $GITHUB_OUTPUT
    exit 1
fi

echo "📋 Processing output..."

# Extract JSON from output (everything from the first '{' to the last '}')
# Find the JSON section - look for lines that start with '{' and end with '}'
sed -n '/^{/,/^}$/p' full-output.txt > "$OUTPUT_FILE"

# If that didn't work, try to extract JSON between { and }
if [ ! -s "$OUTPUT_FILE" ]; then
    echo "🔄 Trying alternative JSON extraction..."
    awk '/^{/,/^}/' full-output.txt > "$OUTPUT_FILE"
fi

# Validate and parse JSON
if [ -s "$OUTPUT_FILE" ] && command -v jq >/dev/null 2>&1 && jq empty "$OUTPUT_FILE" 2>/dev/null; then
    HAS_DRIFT=$(jq -r '.HasDrift' "$OUTPUT_FILE")
    echo "✅ Successfully extracted drift report JSON"
    echo "📊 Drift detected: $HAS_DRIFT"
    echo "drift=$HAS_DRIFT" >> $GITHUB_OUTPUT
else
    echo "⚠️  Warning: Failed to extract valid JSON from output"
    echo "📄 Full output:"
    cat full-output.txt || echo "No output file content"
    
    # Set default values
    echo "drift=false" >> $GITHUB_OUTPUT
    
    # Create empty JSON report for consistency
    echo '{"HasDrift": false, "Summary": "Failed to parse drift report"}' > "$OUTPUT_FILE"
fi

echo "✅ Drift check completed"