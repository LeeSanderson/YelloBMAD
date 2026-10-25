// The client on Azure Static Web Apps, Free tier (AC1, AC13). Free has no fixed monthly cost, and it
// supports custom domains. The linked-backend feature that would make the API same-origin needs the
// Standard plan and would change AD-7, so it is not used (Finding F2).

param tags object

@description('Optional custom hostname for the client (P4). Empty uses the default hostname.')
param customHostname string

// Static Web Apps is offered in a handful of regions only; the content is served from its global
// edge either way, so this is fixed rather than following the resource group.
#disable-next-line no-hardcoded-location
var staticSiteLocation = 'westeurope'

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'yello-client'
  location: staticSiteLocation
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

resource domain 'Microsoft.Web/staticSites/customDomains@2023-12-01' = if (!empty(customHostname)) {
  parent: site
  name: empty(customHostname) ? 'unused.invalid' : customHostname
  properties: {}
}

output defaultHostname string = site.properties.defaultHostname
