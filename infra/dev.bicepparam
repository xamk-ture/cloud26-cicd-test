// Dev environment parameters.
// No password parameter any more: the app and the admins use Entra tokens.
using 'main.bicep'

param yourName = 'ture08'
param ownerEmail = 'hture01@xamk.fi'
param environment = 'dev'
// Fresh pay-as-you-go subscription: F1 quota is 0 in North Europe, West Europe takes no
// new customers, and PostgreSQL Burstable is refused in Sweden Central and Poland Central.
// France Central accepts both. Resources may live in a different region than the resource group.
param location = 'francecentral'
// Assignment 7 (cost management): back to F1 after the rightsizing decision — the course environment does not need B1 between sessions.
param appServiceSku = 'F1'
