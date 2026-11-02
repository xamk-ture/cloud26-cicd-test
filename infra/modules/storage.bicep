// =============================================================
// Assignment 4 — Model solution (Exercise 2): storage module
//
// Storage account + posters container for event poster images
// (introduced in Assignment 3, guided section G).
//
// Note: no output carries the account key — module outputs end up
// in deployment history, so secrets must not pass through them.
// main.bicep reads the key with an 'existing' reference + listKeys().
// =============================================================

@description('Storage account name — lowercase letters and digits only, no hyphens, 3-24 chars')
param storageAccountName string

param location string

param tags object = {}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS' // 3 copies in one datacenter — right for dev/course use
  }
  kind: 'StorageV2'
  tags: tags
  properties: {
    allowBlobPublicAccess: false // nothing in this account can be made public, even by accident
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource postersContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'posters'
  properties: {
    publicAccess: 'None' // the app serves the posters; the container stays private
  }
}

output storageAccountName string = storageAccount.name
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
