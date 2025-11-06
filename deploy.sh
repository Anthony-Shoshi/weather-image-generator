#!/bin/bash

# === CONFIGURATION ===
FUNCTION_PROJECT_PATH="./src"
FUNCTION_APP_NAME="weatherimagefa2025"  # your Azure Function App name
RESOURCE_GROUP_NAME="WeatherAppRG"      # your Azure Resource Group

# Optional: override from CLI arguments
if [ ! -z "$1" ]; then FUNCTION_PROJECT_PATH="$1"; fi
if [ ! -z "$2" ]; then FUNCTION_APP_NAME="$2"; fi
if [ ! -z "$3" ]; then RESOURCE_GROUP_NAME="$3"; fi

echo "Publishing Azure Function from: $FUNCTION_PROJECT_PATH"

# Check if host.json exists
if [ ! -f "$FUNCTION_PROJECT_PATH/host.json" ]; then
    echo "Error: host.json not found in $FUNCTION_PROJECT_PATH"
    exit 1
fi

# Navigate to the function project
cd "$FUNCTION_PROJECT_PATH" || { echo "Failed to change directory."; exit 1; }

# Build and publish function
echo "🚀 Building and publishing..."
dotnet publish -c Release -o ./publish

# Deploy using Azure Functions Core Tools
echo "Deploying to Azure Function App: $FUNCTION_APP_NAME"
func azure functionapp publish "$FUNCTION_APP_NAME" --csharp --force

echo "Deployment complete!"