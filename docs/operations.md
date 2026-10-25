# Yello operations record

This file is the operational record for Yello's deployment. Story 1.10 wrote it on 2026-10-25. It holds the release conditions the PRD states as testable, the cost ceiling, the first-deploy runbook, and a list of what nobody has observed yet.

`tests/Yello.Tests.Architecture/OperationsRecordTests.cs` reads this file. If an edit drops one of the five §6.4 prerequisites, the AR-40c rule or a prerequisite's state, the build fails.

**Nothing in this file claims that a deployment happened.** The first real deploy is an operator act after story 1.10. Section 11 lists everything that only Azure can show.

---

## 1. Environments

There are exactly two environments:

| Environment | How it runs | Configuration |
|---|---|---|
| **Local** | `./run.ps1` (`dotnet aspire run`): SQL Server 2025 in a container, then the `migrate` resource (it applies the migrations and exits), then `Yello.Host` and `Yello.Client`. | `ASPNETCORE_ENVIRONMENT=Development`. Aspire injects the connection string. There are no secrets in source. |
| **Azure** | `infra/main.bicep`, deployed by the operator. Every release goes through `.github/workflows/ci.yml` on a push to `main`. | `ASPNETCORE_ENVIRONMENT=Production`. Every value comes from an environment variable, and the connection string is a Key Vault reference. |

There is no staging environment and no second Azure environment (AC4). A second Azure environment would use the same free grants that make production free. The workflow names one GitHub environment, `azure`, and the infrastructure has no environment parameter. A build gate checks both facts.

## 2. §6.3 cost ceiling: £30 per month

**This is an operational condition alongside the §6.4 gate below. It is not a backlog item.**

Mechanisms:

- **The free offer pauses rather than bills.** The database uses `useFreeLimit: true` with `freeLimitExhaustionBehavior: 'AutoPause'` (AC5). When the month's 100,000 vCore seconds run out, the database pauses until the next calendar month. Microsoft's page says that once "continue with charges" is chosen, it **cannot be switched back**, so never choose it.
- **An alert fires at 10% of the free allowance.** A metric alert on `free_amount_remaining` (portal name "Free amount remaining") fires at `<= 10000` vCore seconds, which is 10% of 100,000, and emails the operator. Source: <https://learn.microsoft.com/azure/azure-sql/database/free-offer>, read 2026-10-25. That page suggests exactly this alert and says it is free.
- **A budget covers the resource group.** The budget is £30 per month (in the billing currency) and alerts at 50%, 80% and 100% (open question 16).
- **Logs are capped.** The Log Analytics daily ingestion cap is 0.1 GB, so logging stays inside the free ingestion allowance.

Cost table. These are estimates made on 2026-10-25 from Microsoft's published pricing and free grants, without a subscription. Confirm each line in the Azure pricing calculator at P1.

| Line | Free grant / basis | `minReplicas: 0` | `minReplicas: 1` |
|---|---|---|---|
| Azure SQL Database, serverless GP, free offer | 100,000 vCore s, 32 GB data and 32 GB backup per database per month. It auto-pauses at the limit, so it never bills. | £0 | £0 |
| Container Apps, consumption, 0.5 vCPU / 1 GiB | Monthly per-subscription grant: 180,000 vCPU s, 360,000 GiB s, 2 million requests | ≈ £0 (scales to zero; sparse traffic stays inside the grant) | ≈ £12–15 (always-on idle rate; spine `:425` says ~$12–15) |
| Container Apps Job `yello-migrate` | Same grant. It runs for seconds per release. | ≈ £0 | ≈ £0 |
| Container Registry, Basic | No free grant. About US$0.167 per day. | ≈ £4 | ≈ £4 |
| Static Web Apps, Free | Free plan | £0 | £0 |
| Key Vault, standard | US$0.03 per 10,000 operations. References are resolved at revision start. | ≈ £0 | ≈ £0 |
| Log Analytics | 5 GB per month of free ingestion; the cap is 0.1 GB per day (≤ 3.1 GB per month) | £0 | £0 |
| Metric alerts (free amount remaining) | Microsoft says a metric alert on this metric is free | £0 | £0 |
| Log search alert (rate-limit refusals, every 15 minutes; open question 14) | About US$0.50 per rule per month at a 15-minute frequency. Confirm at P1. | ≈ £0.40 | ≈ £0.40 |
| Action group email | The first 1,000 emails per month are free | £0 | £0 |
| Budget | Free | £0 | £0 |
| **Total** | | **≈ £4.50 of £30** | **≈ £17–20 of £30** |

The Container Registry line is the largest fixed cost. It buys image pulls by managed identity with no stored registry credential (open question 2 (a)). GitHub Container Registry would be free, but Container Apps would then need a stored credential to pull from it.

## 3. §6.4 data-protection gate (AC8)

Recorded as an **operational condition**, alongside the §6.3 cost ceiling, rather than as a backlog item: the PRD wrote it as a testable release condition, and nothing else in this plan detects it.

**The assertion.** This is a **single-operator deployment** claiming **no data-protection posture**. **The first Account created by anyone other than the operator makes the PRD non-compliant until amended.** (PRD §6.4, `prd.md:782-796`.)

The five prerequisites that become live at that moment:

| # | Prerequisite at the gate | State |
|---|---|---|
| 1 | A **lawful basis** for holding email addresses and authored content | Not live yet. It becomes live at the gate. |
| 2 | A stated **data region**, and no replication outside it. The `location` parameter in `infra/main.bicep` is incidental and is **not** this statement. The database's backup storage redundancy is set to `Local`, but nothing has been asserted. | Not live yet. It becomes live at the gate. |
| 3 | **Encryption at rest** asserted. NFR-6 covers transit only. Whatever Azure SQL Database provides by default is incidental. The Data Protection key ring in `dbo.DataProtectionKey` is stored unencrypted, on purpose, under this item. | Not live yet. It becomes live at the gate. |
| 4 | A **breach-notification** position. A verified cross-Space disclosure would be notifiable by definition. | Not live yet. It becomes live at the gate. |
| 5 | A **subject-access** or export route. FR-3 covers erasure only. | Not live yet. It becomes live at the gate. |

**Who confirms it.** The developer wrote this assertion. It is the operator's to confirm or amend before the first deploy (P7).

## 4. The tripwire (R11)

The gate fires when a second person registers. Detect it with an **operator-side datastore query**. Run it as the Entra administrator, in the portal query editor or with `sqlcmd`, and close the connection afterwards (see section 10):

```sql
SELECT COUNT(*) FROM dbo.Account;
```

A result greater than 1 means the gate has fired: section 3's prerequisites are now live, and the PRD is non-compliant until it is amended.

This is a direct datastore query and **never an endpoint**. PRD §6.1 forbids an in-product aggregate that spans Spaces. An endpoint that counted Accounts would be the third non-Space-scoped surface that AD-24 refuses. Do not build one.

## 5. The obligations parked against the gate (AC9)

**Browser spellcheck and cloud IME egress.** Several browsers' enhanced spellcheck, and several mobile cloud IMEs (input methods), send the description text a person types to a third-party service. This egress is **accepted for v1**. It becomes a real obligation at the gate. It is recorded here and not left in `EXPERIENCE.md:181` alone, because it was deliberately parked "against §6.4's data-protection gate". A plan that carries the obligation without the gate loses both. The description editor is also **the product's largest free-text surface**.

**Subject access and export.** `addendum.md:164` holds an Account-scoped export "behind the §6.4 gate". It is the same item as prerequisite 5 above.

## 6. AR-40c: the `SESSION_CONTEXT` parallel-plan defect (AC7)

**Status: not confirmed. `MAXDOP = 1` is in force.** On SQL Server, the defect is documented across 2019 CU14–CU31, 2022 CU1–CU23 and 2025 RTM–CU2. A parallel plan that reads `SESSION_CONTEXT()` on a pool-reset session can return another tenant's rows without any error. Nobody has established whether Azure SQL Database is affected (spine `:427`). AD-2 removes the class with the database-scoped `MAXDOP = 1` that migration `20260925132825_ActiveSpaceResolution` sets.

Procedure (P5), before the first production deploy:

1. Confirm Azure SQL Database's exposure directly. Use a Microsoft support ticket or a written vendor statement. Record the answer, the date and the reference here.
2. If Azure SQL Database is affected, or the answer is unclear, `MAXDOP = 1` stays. Stop here.
3. If it is unaffected, run the **pooled-connection** isolation case (`Yello.Tests.Isolation` `PooledConnectionCaseTests`) against a scratch free-offer database on the deployed server. Never run it against the production database: the case registers Accounts, and that would fire the §6.4 tripwire. A subscription allows ten free databases. Create a scratch database with `az sql db create ... --use-free-limit true --free-limit-exhaustion-behavior AutoPause`, migrate it with the bundle, and run the case against it. The suite does not yet have a way to target an external database: `deferred-work.md` holds that against P5. Delete the scratch database afterwards.
4. Relax the setting only with a **new migration** that sets `MAXDOP = 0`, and only if that pooled-connection case is green. Never change it in the portal: the setting travels with the schema.
5. Relaxing MAXDOP changes the cold-path latency that story 2.9 measures (R3, `test-design-architecture.md:474-488`). Re-measure after the change.

## 7. AR-40b: cold start against NFR-5 (open question 6)

**Provisional statement: NFR-5 is measured warm, and the cold path is exempt until story 2.9 measures it.** The Host runs with `minReplicas: 0`, so the cost stays near zero (section 2). A request after an idle spell pays two costs: the container's cold start, and the database's resume if it auto-paused after 15 idle minutes (up to about a minute). Story 2.9 owns the measurement and the decision. One parameter, `minReplicas`, moves to 1 at about £12–15 per month. This statement is recorded here so the exemption is never silent (spine `:425`).

## 8. NFR-6: encryption in transit

| Hop | What carries TLS |
|---|---|
| Browser → client | Static Web Apps serves HTTPS only on its default and custom hostnames. |
| Browser → API | Container Apps ingress serves HTTPS with `allowInsecure: false`, so plain http is redirected or refused. The Host sends HSTS outside Development. |
| Ingress → Host container | Inside the Container Apps environment. TLS ends at ingress. The Host learns the original scheme from `X-Forwarded-Proto` (`ForwardedHeaders__Enabled=true`), and that is why the `Secure` cookies and the anti-forgery token still work. |
| Host / migration job → Azure SQL | SqlClient's `Encrypt=True` (mandatory) and `TrustServerCertificate=False` are in both connection strings in Key Vault. The server's minimal TLS version is 1.2. |
| Container Apps → Key Vault, registry | Azure-managed TLS through the managed identities. |

Encryption at rest is **not** asserted. It is §6.4 prerequisite 3.

## 9. Identities and permissions

| Identity | Azure roles | In the database | Why |
|---|---|---|---|
| `id-yello-app` (the Host) | `AcrPull` on the registry, `Key Vault Secrets User` on the vault | Contained user `yello_app`: `db_datareader` and `db_datawriter` only | It cannot disable row-level security, alter a security policy or change a database-scoped configuration. `DatabaseIdentityTests` proves this locally with the same roles. |
| `id-yello-migrate` (the job) | `AcrPull`, `Key Vault Secrets User` | Contained user `yello_migrate`: `db_owner` | The migrations create functions and security policies and set `ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 1`, which needs `ALTER ANY DATABASE SCOPED CONFIGURATION` (`deferred-work.md:242`). |
| `id-yello-deploy` (the GitHub workflow, by OIDC) | `AcrPush` on the registry. `Contributor` on `yello-host`, `yello-migrate` and `yello-client` only. | None | It pushes images, updates the job and the app, starts the job, and reads the Static Web App's deployment token. It holds no data access. |
| The Entra administrator (Lee) | Owner of the resource group, for the bootstrap | Server administrator | The bootstrap, the tripwire query and AR-40c only. |

**SQL authentication (open question 8 (b)).** Both runtime identities connect as contained database users with passwords. The passwords exist only in the two Key Vault secrets and in the operator's hands during the bootstrap. Entra managed-identity authentication (8 (a)) was not taken, because `Microsoft.Data.SqlClient` 7.0.2 needs another package, `Microsoft.Data.SqlClient.Extensions.Azure`, for any Entra authentication method. The driver's own error text says so. Adding that package needs Lee's approval. If Lee approves it, change the two connection strings to `Authentication=Active Directory Managed Identity;User Id=<client id>` and drop the passwords.

**The firewall trade-off.** Consumption Container Apps has no fixed outbound address. The SQL server therefore admits "Azure services" (`0.0.0.0`). That means any Azure-hosted client can reach the login prompt, and only a credential stops it. The alternative is a VNet-integrated environment with a NAT gateway, which costs money that §6.3 does not have.

**Expand/contract rule.** The deploy runs the migration job while the **previous** revision is still serving. Every migration must therefore work with the code of the revision before it. Add columns as nullable or with a default. Drop or rename only in a later release, after no running revision reads the old shape. A migration that breaks the running revision is a defect. All eight migrations in the repository, checked on 2026-10-25, comply. Their `Up` steps add tables, indexes, constraints, predicates, a database-scoped configuration and a nullable column. Two steps change something that already existed, and both stay compatible with the previous revision. `AccountScopedMembership` swaps Membership's filter predicate for a wider one (Space *or* Account), which still admits every row a Space-scoped request saw before. `ApiTokens` replaces an index with a unique constraint on the same columns, which keeps uniqueness throughout.

## 10. First-deploy runbook

Do the steps in order. Record each prerequisite's state in the table at the end. Never mark one as provided on Lee's behalf.

**Warning about open connections.** An open SSMS or query-editor connection, including Object Explorer, stops auto-pause and uses up the free vCore seconds. Close every connection when you finish.

### P1: subscription, region and alert address

```bash
az login
az account set --subscription <subscription-id>
az group create --name <rg> --location <region>
```

Copy `infra/main.parameters.example.json` to a file **outside the repository**, then fill in the values. None of them is secret.

### P2: Entra rights and the bootstrap

Run the deployment as Owner of the resource group, or as Contributor plus User Access Administrator, because it creates role assignments. Generate three passwords locally, and never commit them:

```bash
sqlAdmin=$(openssl rand -base64 24)
appPw=$(openssl rand -base64 24)
migratePw=$(openssl rand -base64 24)
server=<namePrefix>-sql.database.windows.net
appConn="Server=tcp:$server,1433;Initial Catalog=yello;User ID=yello_app;Password=$appPw;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60"
migrateConn="Server=tcp:$server,1433;Initial Catalog=yello;User ID=yello_migrate;Password=$migratePw;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60"

az deployment group create --resource-group <rg> --template-file infra/main.bicep \
  --parameters @<your-parameters-file> \
  --parameters sqlAdministratorPassword="$sqlAdmin" appConnectionString="$appConn" migrateConnectionString="$migrateConn"
```

`Connect Timeout=60` covers a paused serverless database while it resumes (error 40613 is the transient code). Without it, the first request after a pause fails before the database answers. The Host answers such a failure with a uniform `503` and `Retry-After`, never a `500`.

On the first deployment, the Container App runs the placeholder `hostImage` until the first workflow run replaces it (see section 11). When you deploy this template again later, pass `hostImage=<registry>/yello-host:<current sha>` and `migrateImage=...`. Otherwise the deployment resets the images to the placeholders. You can omit the connection-string parameters to leave the stored secrets unchanged.

Next, connect as the Entra administrator to the database `yello` and create the two contained users:

```sql
CREATE USER [yello_app] WITH PASSWORD = '<appPw>';
ALTER ROLE db_datareader ADD MEMBER [yello_app];
ALTER ROLE db_datawriter ADD MEMBER [yello_app];

CREATE USER [yello_migrate] WITH PASSWORD = '<migratePw>';
ALTER ROLE db_owner ADD MEMBER [yello_migrate];
```

Then forget the passwords. Key Vault holds the only copies. Keep `sqlAdmin` in your password manager. It is only needed if the Entra administrator is ever lost.

### P3: GitHub deploy trust

1. In the repository settings, create the environment **`azure`**. Optionally, require a reviewer.
2. Add these repository variables. They are variables, not secrets, and none of them is a credential:
   - `AZURE_CLIENT_ID`: the output `deployIdentityClientId`
   - `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
   - `YELLO_RESOURCE_GROUP`: `<rg>`
   - `YELLO_REGISTRY_NAME`: the registry's name, which is `registryLoginServer` without `.azurecr.io`
   - `YELLO_API_HOSTNAME`: optional, the custom API hostname (P4)

   The story planned three variables. The workflow needs the six above, because resource names are deploy-time configuration too.
3. The infrastructure creates a federated credential that trusts `repo:<owner>/<repo>:environment:azure`. No Azure secret is stored in GitHub.

### P4: a domain, if Safari and Firefox must work (Finding F2)

The default hostnames put the client (`*.azurestaticapps.net`) and the API (`*.azurecontainerapps.io`) on **different sites**. The `SameSite=None; Secure` Session cookie is therefore a third-party cookie. Chromium accepts it. **Safari blocks it by default.** Firefox partitions it, and nobody has verified whether that still works. The fix is one registrable domain for both, such as `app.<domain>` and `api.<domain>`. That costs a domain (about £10 per year) and two DNS records. Steps:

1. Deploy again with `clientCustomHostname=app.<domain>` and `apiCustomHostname=api.<domain>`. Add a CNAME from `app` to the Static Web App's default hostname, and validate it as the portal instructs.
2. Bind the API hostname with a free managed certificate:

   ```bash
   az containerapp hostname add --name yello-host --resource-group <rg> --hostname api.<domain>
   az containerapp hostname bind --name yello-host --resource-group <rg> --hostname api.<domain> --environment cae-yello --validation-method CNAME
   ```

3. Set `YELLO_API_HOSTNAME=api.<domain>`.

AD-7 does not change: the origins stay distinct, and CORS still allows exactly the client origin.

### P5: AR-40c

Follow section 6.

### P6: the first run and the first measurements

1. Push to `main`, or re-run the workflow. Watch the jobs `test`, `build-images`, `migrate`, `promote` and `client`, in that order. The `migrate` job fails the run unless the job execution reports `Succeeded`.
2. Open the client. Register, sign in, and see the Personal Space.
3. Record here:
   - **Sign-in p95 on the deployed hardware** (`deferred-work.md:147`). The figure was 220,000 PBKDF2 iterations at about 270 ms on the development machine, and 0.5 vCPU may cross NFR-5's 500 ms. **Do not change `PasswordWorkFactor.ChosenIterationCount` because of this measurement.** `deferred-work.md:225` makes the timing-oracle remedy a precondition of any raise.
   - **One cold-start observation** for AR-40b. Measure the first request after the app has scaled to zero, with the database paused and with it awake.
   - The hosted CI wall clock for the `test` job. The target is 15 minutes.

### P7: the §6.4 signature

Read section 3. Confirm it or amend it before the first deploy, and record which.

### Prerequisite states

| Prerequisite | State | Recorded |
|---|---|---|
| P1: subscription, region, alert address | not yet | 2026-10-25, story 1.10 |
| P2: Entra rights, bootstrap, the two database users | not yet | 2026-10-25, story 1.10 |
| P3: GitHub `azure` environment, federated credential, repository variables | not yet | 2026-10-25, story 1.10 |
| P4: a domain for Safari and Firefox | not yet | 2026-10-25, story 1.10 |
| P5: AR-40c confirmation and the pooled-connection check | not yet | 2026-10-25, story 1.10 |
| P6: first deploy, sign-in p95, cold-start observation | not yet | 2026-10-25, story 1.10 |
| P7: the operator's confirmation of the §6.4 assertion | not yet | 2026-10-25, story 1.10 |

## 11. What has not been observed yet

Nothing below has been seen working. Each item needs an Azure subscription, and each stays "not observed" until the operator observes it and records the date here.

- That `infra/main.bicep` deploys at all. Only `az bicep build` and its linter have run, both with zero findings, using Bicep CLI 0.45.15. `what-if` has not run.
- That the first deployment succeeds while the Container App runs the placeholder image. The placeholder answers neither probe, so its revision may report unhealthy until the first workflow run replaces it. If the deployment refuses to complete, first deploy with the Host's container removed, or push an image to the registry and pass it as `hostImage`.
- That ingress appends `X-Forwarded-For` and `X-Forwarded-Proto` the way the Host assumes, so a browser can sign in on the deployed client.
- That the Key Vault secret references resolve through the managed identities, and that the container starts with `ConnectionStrings__yello` populated.
- That the contained-user SQL authentication works for the app and migrate identities.
- That the `yello-migrate` job runs, that the workflow waits for `Succeeded`, and that promotion follows.
- That the metric `free_amount_remaining` exists under that name, that the alert reaches the action group, and that the database auto-pauses at exhaustion.
- That the £30 budget alerts at 50%, 80% and 100%.
- That the rate-limit log alert's query matches the console log table, and what it actually costs.
- That the platform's probes report the Host healthy on `/health/live` and `/health/ready`.
- That a Session survives a cold start on the platform (AC12). It is proved locally with fresh per-process profiles.
- Sign-in p95 and a cold-start observation (P6).
- AR-40c (P5).
- That Safari and Firefox keep the Session cookie on the real hostnames (P4).
- That the first hosted CI run is green, finishes in under 15 minutes, and fails rather than skips without Docker.
- That the operator has confirmed the §6.4 assertion (P7).

### Known consequences, recorded rather than fixed

- **The key ring wakes the database once per cold process.** The Data Protection key ring lives in the database. The first request of a new process that issues or reads a cookie or an anti-forgery token therefore reads it once, and that includes an anonymous `GET /api/v1/antiforgery/token`. After that the process keeps the ring in memory. Probes, the unmatched `/api` fallback and anonymous `401`s never read it, and none of them starts the refusal purge.
- **The single-revision swap briefly runs two processes.** During the overlap, an in-process push and the `/sync` lease cannot assume a single process. `deferred-work.md` holds this against story 7.3.
