#!/bin/bash
set -e

# === CONFIGURATION ===
RESOURCE_GROUP_NAME="AssignmentWeatherAppRG"
LOCATION="Spain Central"
BICEP_FILE="./bicep/main.bicep"
FUNCTION_PROJECT_PATH="./src"

# Allow optional args for overrides
if [ ! -z "$1" ]; then RESOURCE_GROUP_NAME="$1"; fi
if [ ! -z "$2" ]; then LOCATION="$2"; fi

echo "Deploying Azure infrastructure using Bicep..."
az group create --name "$RESOURCE_GROUP_NAME" --location "$LOCATION"

# Deploy the Bicep template
az deployment group create \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --template-file "$BICEP_FILE" \
  --output json \
  --query "properties.outputs" > deployment_outputs.json

echo "Infrastructure deployed successfully!"

# Extract values from outputs
FUNCTION_APP_NAME=$(jq -r '.functionAppName.value' deployment_outputs.json)
STORAGE_ACCOUNT_NAME=$(jq -r '.storageAccountName.value' deployment_outputs.json)

echo "Function App: $FUNCTION_APP_NAME"
echo "Storage Account: $STORAGE_ACCOUNT_NAME"

# === FUNCTION DEPLOYMENT ===
echo "Publishing Azure Function from: $FUNCTION_PROJECT_PATH"

if [ ! -f "$FUNCTION_PROJECT_PATH/host.json" ]; then
    echo "Error: host.json not found in $FUNCTION_PROJECT_PATH"
    exit 1
fi

cd "$FUNCTION_PROJECT_PATH"

echo "Building function..."
dotnet publish -c Release -o ./publish

echo "Deploying to Azure Function App: $FUNCTION_APP_NAME"
func azure functionapp publish "$FUNCTION_APP_NAME" --dotnet-isolated --force

echo "Deployment complete!"