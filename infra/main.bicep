@description('Organisation name — used as a prefix on all resource names.')
param orgName string = 'paymenow'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Strava application client ID.')
param stravaClientId string

@description('Strava application client secret.')
@secure()
param stravaClientSecret string

@description('The callback URL Strava will redirect to after auth.')
param stravaRedirectUri string

@description('Strava club ID to verify membership against.')
param stravaClubId string

// ── Naming ────────────────────────────────────────────────────────────────────
var appName = '${orgName}-wellnessclub'
var storageAccountName = '${orgName}wellnessclubsa' // must be lowercase, max 24 chars

// ── Storage Account ───────────────────────────────────────────────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// ── App Service Plan (F1 — free) ──────────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: false
  }
}

// ── App Service ───────────────────────────────────────────────────────────────
resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      appSettings: [
        {
          name: 'Strava__ClientId'
          value: stravaClientId
        }
        {
          name: 'Strava__ClientSecret'
          value: stravaClientSecret
        }
        {
          name: 'Strava__RedirectUri'
          value: stravaRedirectUri
        }
        {
          name: 'Strava__ClubId'
          value: stravaClubId
        }
        {
          name: 'Azure__StorageConnection'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
      ]
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
