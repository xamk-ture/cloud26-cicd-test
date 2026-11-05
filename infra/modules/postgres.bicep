// =============================================================
// PostgreSQL Flexible Server module:
//   server + application database + Azure-services firewall rule.
//
// Deliberately NOT included: a firewall rule for the developer's
// own IP. Laptop IPs change; a stale IP in versioned code misleads.
// That rule stays a CLI operation:
//   az postgres flexible-server firewall-rule create ...
// =============================================================

param location string
param serverName string
param databaseName string
param tags object = {}

// --- PostgreSQL Flexible Server (Burstable B1ms — course standard) ---

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: serverName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled' // contract phase: tokens only — campusadmin is gone
      tenantId: subscription().tenantId
    }
    network: {
      // App Service is not in a VNet. The firewall rule only works if public access is on.
      // Stating it here also stops what-if from predicting a delete of this property.
      publicNetworkAccess: 'Enabled'
    }
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled' // HA doubles the price — not for course projects
    }
  }
  tags: tags
}

// --- The application database ---

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// --- Firewall: allow Azure services (the special 0.0.0.0 rule) ---

resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverFqdn string = postgresServer.properties.fullyQualifiedDomainName
