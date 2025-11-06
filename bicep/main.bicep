param location string = resourceGroup().location

var prefix = uniqueString('weatherimg2025', location, resourceGroup().name, subscription().subscriptionId)
var serverFarmName = '${prefix}-sf'
var functionAppName = 'weatherimagefa2025'
var storageAccountName = 'weatherimgstorage2025'

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

// Queues
resource startQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: storageAccount
  name: 'default/start-queue'
}

resource imageQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: storageAccount
  name: 'default/image-queue'
}

// Blob container
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: storageAccount
  name: 'default/generated-images'
  properties: {
    publicAccess: 'None'
  }
}

// Function App plan (Consumption)
resource serverFarm 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: serverFarmName
  location: location
  sku: {
    tier: 'Consumption'
    name: 'Y1'
  }
  kind: 'elastic'
}

// Function App
resource functionApp 'Microsoft.Web/sites@2021-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    enabled: true
    serverFarmId: serverFarm.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
          value: '1'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageAccount.listKeys().keys[0].value
        }
        {
          name: 'START_QUEUE_NAME'
          value: 'start-queue'
        }
        {
          name: 'IMAGE_QUEUE_NAME'
          value: 'image-queue'
        }
        {
          name: 'BUVIENRADAR_FEED'
          value: 'https://data.buienradar.nl/2.0/feed/json'
        }
        {
          name: 'UNSPLASH_ACCESS_KEY'
          value: '' // fill this after deployment
        }
      ]
    }
  }
}

output storageAccountName string = storageAccount.name
output functionAppName string = functionApp.name
output startQueueName string = startQueue.name
output imageQueueName string = imageQueue.name
output blobContainerName string = blobContainer.name