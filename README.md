# ☁️ Weather Image Generator – Azure Function App

This project is an **Azure Function App** deployed using an **Azure Bicep template** and a **deployment script**.  
It automates provisioning Azure resources and publishing the function app code in one step.

---

## 🚀 Prerequisites

Make sure you have the following installed:

| Tool | Description | Check Command |
|------|--------------|----------------|
| **Azure CLI** | To manage Azure resources | `az version` |
| **Bicep CLI** | For ARM template deployments | `az bicep version` |
| **.NET SDK (8.0+)** | To build and publish the function app | `dotnet --version` |
| **Azure Functions Core Tools (v4)** | To deploy and test locally | `func --version` |
| **PowerShell** | For Windows/macOS users running `deploy.ps1` | `pwsh --version` |
| **jq (optional)** | Required only if using `deploy.sh` | `jq --version` |

---

## ⚙️ Configuration

You can customize resource names and deployment regions in the scripts.

Default values used in both deploy scripts:
```bash
RESOURCE_GROUP_NAME="AssignmentWeatherAppRG"
LOCATION="Spain Central"
```

## 🧩 Deployment Steps

Option 1: Using PowerShell (Windows/macOS)
```bash
# From project root
pwsh ./deploy.ps1
```

Option 2: Using Bash (Linux/macOS)
```bash
# From project root
chmod +x deploy.sh
./deploy.sh
```

After deployment completed successfully, add **UNSPLASH_ACCESS_KEY** from Azure portal inside azure function app settings. We are putting this manually for security reason. You will get this access key from https://unsplash.com/developers

## 🔍 Verify Deployment
After deployment completes, check your Function App in the Azure Portal:

- Go to: https://portal.azure.com

- Navigate to Resource Groups → YourResourceGroup

- Open the Function App

- Check “Functions” tab — you should see your function listed and running.

## 🌦️ API Testing
You can test your deployed API endpoints from **http/weather-api.http** file.

## Github Actions for CI/CD
Github action configuration added to Build and deploy the code automatically when code push to master branch. But make sure you have added **AZURE_FUNCTIONAPP_PUBLISH_PROFILE** in secret and value will be publish profile file value. You will get this file from Azure Portal.
