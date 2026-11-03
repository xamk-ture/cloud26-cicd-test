// =============================================================
// Assignment 4 — Model solution: Campus Events environment
//
// Describes the desired state of the whole environment:
//   App Service Plan + Web App + PostgreSQL Flexible Server
//   + Storage account (posters container).
// Deploys into an EXISTING resource group (Assignment 1 created
// it, together with the budget alert — those are not managed here).
//
// Deploy:
//   az deployment group what-if  -g <rg> -f main.bicep -p dev.bicepparam
//   az deployment group create   -g <rg> -f main.bicep -p dev.bicepparam
// (or use deploy.ps1, which wraps both with a confirmation)
// =============================================================

@description('Your name in lowercase, no special characters — used in resource names')
param yourName string

@description('Owner tag value (school email)')
param ownerEmail string

@description('Environment name — part of every resource name')
param environment string = 'dev'

param location string = resourceGroup().location

@allowed(['F1', 'B1'])
@description('App Service Plan SKU. F1 = free, B1 = paid (scale-out capable)')
param appServiceSku string = 'F1'

@secure()
@description('PostgreSQL admin password — read from POSTGRES_ADMIN_PASSWORD, never stored in a file')
param postgresAdminPassword string

// --- Naming convention (course standard) and shared tags ---

var planName           = 'plan-cloud26-${yourName}-${environment}'
var appName            = 'app-cloud26-${yourName}-${environment}'
var postgresServerName = 'pg-cloud26-${yourName}-${environment}'
var databaseName       = 'campusevents'
var postgresAdminLogin = 'campusadmin'
var storageAccountName = 'stcloud26${replace(yourName, '-', '')}${environment}' // letters + digits only, 3-24 chars

var tags = {
  Environment: environment
  Course: 'cloud26-week5'
  Owner: ownerEmail
  ManagedBy: 'Bicep'
}

// --- PostgreSQL (module) ---

module postgres 'modules/postgres.bicep' = {
  name: 'postgresDeployment'
  params: {
    location: location
    serverName: postgresServerName
    databaseName: databaseName
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    tags: tags
  }
}

// --- Storage account (module — Exercise 2 refactor) ---

module storage 'modules/storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
    storageAccountName: storageAccountName
    tags: tags
  }
}

// The connection string needs the account key, and module outputs must
// not carry secrets (they land in deployment history). An 'existing'
// reference bridges the module boundary; the web app's dependsOn
// waits for the module — 'existing' creates no automatic ordering.
resource storageAccountRef 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// --- App Service Plan ---

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  sku: {
    name: appServiceSku
  }
  kind: 'linux'
  properties: {
    reserved: true // required flag for Linux plans
  }
  tags: tags
}

// --- Web App ---

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  tags: tags
  dependsOn: [
    storage // 'existing' does not wait for the module; the account must exist before listKeys()
  ]
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServiceSku != 'F1' // F1 does not support Always On
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          // Derived from the module output — cannot drift out of sync
          // with the actual server. Still an Application Setting, i.e.
          // the same secrets rung as Assignment 3; Key Vault comes later.
          name: 'ConnectionStrings__DefaultConnection'
          value: 'Host=${postgres.outputs.serverFqdn};Database=${databaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'
        }
        {
          // Storage key read at deploy time — derived, never hand-copied.
          // Same secrets rung as the DB password; Key Vault + Managed Identity
          // removes both later in the course.
          name: 'ConnectionStrings__Storage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccountRef.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
        }
      ]
    }
  }
}

// --- Outputs (read by scripts and pipelines) ---

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output postgresServerFqdn string = postgres.outputs.serverFqdn
output blobEndpoint string = storage.outputs.blobEndpoint
