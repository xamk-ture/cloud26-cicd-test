// Dev environment parameters.
// The password is NOT written here. main.bicep marks it @secure(),
// and this file only points at an environment variable.
// Set it in the same terminal before deploying:
//   $env:POSTGRES_ADMIN_PASSWORD = Read-Host "PostgreSQL admin password"
using 'main.bicep'

param yourName = 'ture08'
param ownerEmail = 'hture01@xamk.fi'
param environment = 'dev'
// Fresh pay-as-you-go subscription: F1 quota is 0 in North Europe, West Europe takes no
// new customers, and PostgreSQL Burstable is refused in Sweden Central and Poland Central.
// France Central accepts both. Resources may live in a different region than the resource group.
param location = 'francecentral'
// Assignment 7 (cost management): B1 so the plan shows up in Cost analysis and has real CPU/memory metrics.
param appServiceSku = 'B1'
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
