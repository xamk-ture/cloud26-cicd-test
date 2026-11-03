// Dev environment parameters.
// The password is NOT written here. main.bicep marks it @secure(),
// and this file only points at an environment variable.
// Set it in the same terminal before deploying:
//   $env:POSTGRES_ADMIN_PASSWORD = Read-Host "PostgreSQL admin password"
using 'main.bicep'

param yourName = 'ture06'
param ownerEmail = 'hture01@xamk.fi'
param environment = 'dev'
// Fresh subscription: F1 quota is 0 in North Europe, West Europe takes no new customers,
// and PostgreSQL B1ms is refused in Sweden Central. Poland Central has both.
// Resources may live in a different region than the resource group.
param location = 'polandcentral'
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
