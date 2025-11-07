param location string = resourceGroup().location

var prefix = 'assignmentweatherimg2025'
var serverFarmName = substring('${prefix}-sf', 0, 16)
var functionAppName = 'assignmentweatherimagefa2025'
var storageAccountName = substring('${prefix}stg', 0, 24) // ensure under 24 chars

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

// Queue service (parent for queues)
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}

// Queues
resource startQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: 'start-queue'
}

resource imageQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: 'image-queue'
}

// Blob service (parent for containers)
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}

// Blob container
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'generated-images'
  properties: {
    publicAccess: 'None'
  }
}

// Function App plan (Consumption)
resource serverFarm 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: serverFarmName
  location: location
  sku: {
    tier: 'Dynamic'
    name: 'Y1'
  }
  kind: 'functionapp'
}

var storageKey = listKeys(storageAccount.id, '2023-01-01').keys[0].value

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
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageKey};EndpointSuffix=${environment().suffixes.storage}'
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
          value: '' // fill manually later for security reason
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