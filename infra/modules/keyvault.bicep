param location string
param keyVaultName string
param tags object = {}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard' // Premium adds HSM-backed keys — not needed here
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true // RBAC, not legacy access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7 // deleted vaults/secrets are recoverable this long
  }
}

output vaultUri string = keyVault.properties.vaultUri
