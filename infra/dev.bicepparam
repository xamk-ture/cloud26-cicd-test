// Dev environment parameters.
// The password is NOT written here. main.bicep marks it @secure(),
// and this file only points at an environment variable.
// Set it in the same terminal before deploying:
//   $env:POSTGRES_ADMIN_PASSWORD = Read-Host "PostgreSQL admin password"
using 'main.bicep'

param yourName = 'ture05'
param ownerEmail = 'hture01@xamk.fi'
param environment = 'dev'
// Fresh subscription: F1 quota is 0 in North Europe and West Europe takes no new customers.
// Resources go to Sweden Central; the resource group itself can stay where it is.
param location = 'swedencentral'
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
