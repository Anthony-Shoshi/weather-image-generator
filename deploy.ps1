# # deploy.ps1

# param(
#     [string]$ResourceGroupName = "WeatherAppRG",
#     [string]$Location = "Spain Central",
#     [string]$BicepFile = ".\bicep\main.bicep",
#     [string]$FunctionProjectPath = ".\src"
# )

# # 1. Login to Azure (if not already)
# az account show > $null 2>&1
# if ($LASTEXITCODE -ne 0) 
#     Write-Host "Logging into Azure..."
#     az login
# }

# # 2. Ensure resource group exists
# if (-not (az group exists --name $ResourceGroupName)) {
#     Write-Host "Creating resource group $ResourceGroupName in $Location"
#     az group create --name $ResourceGroupName --location $Location
# }

# # 3. Deploy Bicep template
# Write-Host "Deploying Bicep template..."
# $deployment = az deployment group create `
#     --resource-group $ResourceGroupName `
#     --template-file $BicepFile `
#     --query "properties.outputs" | ConvertFrom-Json

# $FunctionAppName = $deployment.functionAppName.value
# Write-Host "Function App Name: $FunctionAppName"

# # 4. Publish .NET Function App
# Write-Host "Publishing .NET Function App..."
# dotnet publish $FunctionProjectPath -c Release -o "$FunctionProjectPath\bin\Release\net8.0\publish"

# # 5. Deploy to Azure Function App
# Write-Host "Deploying function code to $FunctionAppName..."
# az functionapp deploy `
#     --name $FunctionAppName `
#     --resource-group $ResourceGroupName `
#     --src-path "$FunctionProjectPath\bin\Release\net8.0\publish" `
#     --type zip

# Write-Host "Deployment completed successfully!"

# ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

# deploy.ps1
param(
    [string]$FunctionProjectPath = ".\src",
    [string]$FunctionAppName = "weatherimagefa2025",  # replace with your actual Function App name
    [string]$ResourceGroupName = "WeatherAppRG"   # replace with your resource group name
)

Write-Host "Publishing Azure Function..."

# Publish function to Azure using dotnet CLI
dotnet publish $FunctionProjectPath -c Release -o $FunctionProjectPath\publish

# Deploy the published files to the existing Function App
func azure functionapp publish $FunctionAppName --csharp --publish-local-settings -i

Write-Host "Deployment complete!"