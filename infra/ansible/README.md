# Tuki Azure-to-GCP deployment and data migration

## Production CD

The supported promotion path is:

```text
feature/* -> dev -> staging -> main
```

`main` represents production. CI runs the .NET build/tests, Docker Compose
validation, and disposable SQL schema/integration tests. On a push produced by
merging a reviewed `staging -> main` pull request, and only after all three jobs
succeed for the same `github.sha`, CI calls `deploy-production.yml`. The
workflow never creates or merges a pull request and cannot run its deploy job
for a pull-request event, another branch, or a SHA different from the caller's
tested SHA. The separate promotion-source ruleset/check remains responsible
for allowing only `dev -> staging` and `staging -> main` promotions.

The reusable production workflow uses `contents: read`, the protected GitHub
environment `production`, strict SSH host-key checking, and a production-only
concurrency group with `cancel-in-progress: false`. The parent CI run is also
not canceled once a `main` or `staging` deployment may have begun.

### GitHub configuration

Create the `production` GitHub environment and store these environment secrets:

- `PRODUCTION_HOST`: SSH hostname or IP of the active production host.
- `PRODUCTION_SSH_USER`: sudo-capable deployment account.
- `PRODUCTION_SSH_PRIVATE_KEY_B64`: base64 of the private key as one line,
  for example `base64 -w 0 /path/to/key` on GNU/Linux.
- `PRODUCTION_SSH_HOST_KEY`: complete trusted `known_hosts` line for
  `PRODUCTION_HOST`, obtained and verified out of band. Do not trust an
  unverified `ssh-keyscan` result.
- `ANSIBLE_VAULT_PASSWORD`: password for the tracked encrypted
  `group_vars/vault.yml`.

The production workflow deliberately uses provider-neutral secret, inventory,
and project names. `group_vars/tuki_production.yml` contains only the current
host's compatibility paths and legacy container fallbacks (`tuki-sql`,
`pelias_api`, and `valhalla`). Change or override that file when production
moves providers; do not rename the generic workflow secrets back to the older
`AZURE_*` staging names.

Configure the `production` environment with required reviewers/manual approval
and restrict deployment branches to `main`. Keep the `main` ruleset configured
for pull requests, required reviews, required CI checks, no bypasses, and the
separate rule/check that permits only `staging` as the promotion source.
Likewise, keep `staging` protected so only `dev` is promoted into it. Repository
settings and environment approvals are intentionally not modified by code.

### Production deployment sequence

The production playbook uses a separate `/opt/tuki/production/AUP` checkout and
verifies its `git rev-parse HEAD` against the exact 40-character main SHA. It
then:

1. renders production-only runtime files and validates that the backend points
   to exactly `Tuki`, never `Tuki_Staging`;
2. validates `docker-compose.production.yml`, creates the private
   `tuki-production-internal` network, and attaches the already-running SQL
   Server, Pelias, and Valhalla containers under production-only aliases;
3. builds `tuki-backend-production:<sha>` and
   `tuki-admin-production:<sha>` before changing the database;
4. requires the existing `Tuki` database, the foundational schema, and a full
   backup recorded by SQL Server within
   `tuki_production_max_backup_age_hours` (24 by default);
5. applies only new, ordered `database/migrations/*.sql` files and records each
   filename plus SHA-256 in `dbo.__TukiSchemaMigrations`;
6. updates only backend/admin with `--no-deps`, checks container health and
   loopback `/health` endpoints (the backend health includes database and
   routing-data checks), and restores the previous recorded app images if a
   local app health check fails;
7. renders a host-Caddy fragment for the existing production hostnames,
   validates a candidate and the installed configuration, then reloads Caddy;
8. verifies both public HTTPS `/health` routes with normal TLS validation and
   records the successfully deployed commit.

The app-only Compose project publishes ports `5140` and `5040` on
`127.0.0.1` only. It does not define, recreate, restart, restore, or expose SQL
Server, Pelias, or Valhalla. The legacy production containers and their volumes
remain in place. Staging keeps its separate checkout, ports, database, network,
and working deployment workflow.

### Database safety and migration caveats

Production CD never creates, restores, drops, or replaces `Tuki`, never reads
from `Tuki_Staging`, and never runs the staging reference-data synchronization.
It does not run the broad base/seed schema during a normal deployment. A host
without the validated foundational tables fails before migration and requires
an explicit operator-led baseline procedure.

Migration files are scanned before any host mutation. A statement beginning
with `DROP`, `TRUNCATE`, `DELETE FROM`, `RESTORE DATABASE`, or `ALTER DATABASE`
causes deployment to fail. Review every new migration for locking, runtime, and
data-conversion risk even when it passes this conservative scan. Applied
migration files are immutable: changing a file whose filename is already in
the ledger causes a checksum failure; add a new migration instead. Existing
repository migrations are additive/idempotent, so the first ledger-enabled run
can safely reconcile them, but it should still be rehearsed against a restored
production backup before enabling CD.

There are no automatic down-migrations or database rollbacks. Maintain and
regularly test SQL Server full/log backups according to the recovery objective.
If a schema release must be reversed, stop writes as appropriate and use the
documented, operator-approved backup/restore procedure on an isolated recovery
target before any production restore decision.

### Application rollback

Successful deployments leave exact SHA-tagged images in Docker and record:

```text
/opt/tuki/production/deployed-commit
/opt/tuki/production/previous-deployed-commit
```

To roll back application code (not the database), read and verify the previous
SHA, then run on the production host:

```bash
cd /opt/tuki/production/AUP
previous="$(sudo cat /opt/tuki/production/previous-deployed-commit)"
test "${#previous}" -eq 40
sudo env TUKI_PRODUCTION_IMAGE_TAG="$previous" \
  docker compose --env-file runtime/production-compose.env \
  -f docker-compose.production.yml up -d --no-build --no-deps \
  backend-production admin-production
curl --fail http://127.0.0.1:5140/health
curl --fail http://127.0.0.1:5040/health
curl --fail "https://$(awk -F': ' '/^tuki_api_hostname:/{print $2}' infra/ansible/group_vars/all.yml)/health"
curl --fail "https://$(awk -F': ' '/^tuki_admin_hostname:/{print $2}' infra/ansible/group_vars/all.yml)/health"
```

After verification, update `deployed-commit` deliberately or redeploy the
desired main commit through the normal reviewed promotion path. Do not attempt
an automatic database downgrade. Docker image pruning must retain at least the
current and previous production SHA tags.

### One-time production setup

Before approving the first production job:

- verify the production SSH key, host-key line, sudo access, and repository
  read access from the host;
- verify host Caddy is active and `tuki_caddyfile_path` is correct;
- reconcile any unmanaged Caddy blocks for the existing production hostnames;
  duplicate site definitions make candidate validation fail safely, so move
  those routes into the managed fragment during a planned cutover;
- verify the configured production hostnames already resolve to this host;
- verify the existing SQL Server, Pelias, and Valhalla container names or
  override the compatibility fallbacks in production inventory variables;
- take and verify a full `Tuki` backup less than 24 hours before deployment;
- rehearse all current migrations against a restored copy of that backup;
- ensure loopback ports `5140` and `5040` are free and remain firewalled from
  public access; and
- configure the GitHub `production` environment, secrets, required reviewers,
  deployment-branch restriction, and branch/ruleset checks described above.

For a safe preflight from `infra/ansible`, run `deploy-production.yml` with
`--syntax-check`; do not use Ansible check mode against production because
shell/Compose discovery and migration semantics cannot be modeled reliably.

The production workflow keeps host preparation, data-service startup, database
migration, application deployment, and verification as separate operator-run
phases:

```text
bootstrap.yml (GCP)
    ↓
prepare-services.yml (GCP data containers only)
    ↓
backup-azure-sql.yml
    ↓
transfer-sql-backup.yml
    ↓
restore-gcp-sql.yml
    ↓
transfer-pelias-data.yml (shared placeholder/interpolation data; never Elasticsearch files)
    ↓
transfer-pelias-index.yml (logical Elasticsearch export/import)
    ↓
operator validates SQL Server, Pelias, and Valhalla data
    ↓
deploy.yml (backend → admin → Caddy)
    ↓
verify.yml
```

Database restore is intentionally absent from ordinary deployment. The restore
playbook refuses to run without an explicit acknowledgement and fails when the
configured target database already exists. It never drops a database and never
uses `WITH REPLACE`.

Pelias migration is also separate from deployment. Its shared-data playbook
copies `data/` and `blacklist/` through a secure controller staging directory,
explicitly excludes `data/elasticsearch`, and publishes verified directories
on GCP. Its index playbook uses a digest-pinned temporary elasticdump container
to export/import settings, mappings, and documents through Elasticsearch's
private container network. It never copies live Elasticsearch filesystem data.

## Controller and inventory setup

Run Ansible from the Debian control laptop. The committed `inventory.ini`
contains only the aliases `azure`, `gcp`, and `production`; it contains no
public IPs, users, or private-key paths. Keep provider and production
credentials independent.

Preferred option: configure both aliases in `~/.ssh/config`:

```sshconfig
Host azure
    HostName AZURE_PUBLIC_IP
    User AZURE_SSH_USER
    IdentityFile /absolute/path/to/azure-private-key

Host gcp
    HostName GCP_PUBLIC_IP
    User GCP_SSH_USER
    IdentityFile /absolute/path/to/gcp-private-key

Host production
    HostName PRODUCTION_PUBLIC_IP
    User PRODUCTION_SSH_USER
    IdentityFile /absolute/path/to/production-private-key
```

Alternatively, copy the example inventory and keep the result local:

```bash
cd infra/ansible
cp inventory.local.ini.example inventory.local.ini
chmod 600 inventory.local.ini
```

`inventory.local.ini` is Git-ignored. CI/CD can generate it from protected
environment variables or secret-file mounts and pass `-i inventory.local.ini`.
Never commit a private key or a generated live inventory.

Before any migration phase, verify and trust both SSH host keys, then test:

```bash
ssh azure
ssh gcp
ansible -i inventory.local.ini tuki_azure -m ping
ansible -i inventory.local.ini tuki_gcp -m ping
```

If SSH aliases are configured, omit `-i inventory.local.ini` from all commands.

## Configuration and secrets

Non-secret deployment and migration defaults live in `group_vars/all.yml`; the
placeholder reference is `group_vars/all.example.yml`. Review these values:

- `tuki_database_name`
- `tuki_staging_database_name`
- `tuki_staging_api_hostname`
- `tuki_staging_admin_hostname`
- `tuki_sql_compose_service`
- `tuki_sql_backup_filename`
- `tuki_azure_compose_root`
- `tuki_azure_backup_dir`
- `tuki_gcp_compose_root`
- `tuki_gcp_backup_incoming_dir`
- `tuki_sql_container_backup_dir`
- `tuki_compose_env`
- `tuki_azure_pelias_project_root`
- `tuki_gcp_pelias_root`
- `tuki_pelias_index_name`
- `tuki_pelias_es_compose_service`
- `tuki_pelias_es_container_fallback` (legacy Azure only; leave empty when Compose discovery works)
- `tuki_pelias_container_fallback` (legacy Azure only)
- `tuki_valhalla_container_fallback` (legacy Azure only)
- `tuki_elasticdump_image` (digest-pinned)

The migration prefers resolving the SQL container with:

```bash
docker compose --env-file runtime/compose.env ps -q sqlserver
```

For an existing legacy Azure SQL container that is not managed by this Compose
project, set `tuki_sql_container_fallback` explicitly for the backup command.
Do not set a fallback when Compose discovery works.

Application and SQL secrets remain in encrypted `group_vars/vault.yml`:

```bash
ansible-vault edit group_vars/vault.yml --vault-password-file .vault-password
```

The SQL migration playbooks do not place the SA password in an Ansible command
line. They read the already-injected `MSSQL_SA_PASSWORD` inside the SQL Server
container and expose it only to `sqlcmd` through `SQLCMDPASSWORD`. Relevant SQL
and Compose validation tasks use `no_log: true`.

Generated `runtime/*.env`, `.vault-password`, `inventory.local.ini`, private
keys, and `*.bak` files must remain outside Git.

## Secret injection into containers

```text
Ansible Vault
    ↓ decrypt during bootstrap
Ansible templates
    ↓
runtime/*.env on server (0600)
    ↓
Docker Compose env_file
    ↓
containers
```

`backend.env`, `sqlserver.env`, `admin.env`, and `caddy.env` are scoped to their
services. `compose.env` contains non-secret interpolation values. Manual Compose
commands must include:

```bash
docker compose --env-file runtime/compose.env <command>
```

## Migration and deployment commands

Run all commands from `infra/ansible`. The examples use an ignored local
inventory; omit its `-i` argument when using SSH aliases.

### 1. Bootstrap GCP

```bash
ansible-playbook -i inventory.local.ini playbooks/bootstrap.yml \
  --vault-password-file .vault-password
```

This installs Docker, creates `/opt/tuki`, persistent data directories, and
mode-`0600` runtime configuration, updates the `dev` checkout, and validates
Compose. It starts no containers.

### 2. Start GCP data-service containers

```bash
ansible-playbook -i inventory.local.ini playbooks/prepare-services.yml \
  --vault-password-file .vault-password
```

This starts SQL Server, Pelias Elasticsearch, Valhalla, and the libpostal,
placeholder, and interpolation containers without restoring or replacing
production data. The Pelias API remains stopped so an empty Elasticsearch node
or missing support data is not presented as ready.

### 3. Back up Azure SQL Server

For a Compose-managed Azure SQL container:

```bash
ansible-playbook -i inventory.local.ini playbooks/backup-azure-sql.yml \
  --vault-password-file .vault-password
```

For the known legacy Azure container, supply its name explicitly rather than
committing it:

```bash
ansible-playbook -i inventory.local.ini playbooks/backup-azure-sql.yml \
  --vault-password-file .vault-password \
  -e tuki_sql_container_fallback=tuki-sql
```

The playbook uses `BACKUP DATABASE ... WITH INIT, COMPRESSION, STATS = 10`,
copies the result to `/opt/tuki/backups`, and verifies its size and SHA256.
Production writes continue during this online backup. If a previous backup
exists, the playbook fails by default. To archive the previous files and create
a deliberate replacement, add:

```bash
-e tuki_confirm_backup_replace=true
```

### 4. Transfer the backup through the controller

```bash
ansible-playbook -i inventory.local.ini playbooks/transfer-sql-backup.yml \
  --vault-password-file .vault-password
```

The transfer path is:

```text
Azure /opt/tuki/backups/Tuki.bak
    ↓ encrypted SSH fetch
controller secure temporary directory
    ↓ encrypted SSH copy
GCP /opt/tuki/backups/incoming/Tuki.bak
```

The controller staging directory is removed in an `always` block, including
when transfer validation fails. Azure, controller, and GCP sizes and SHA256
checksums must match. An existing GCP incoming backup causes a failure; use
`-e tuki_confirm_transfer_replace=true` only when intentionally archiving and
replacing that staged file.

### 5. Restore on GCP

```bash
ansible-playbook -i inventory.local.ini playbooks/restore-gcp-sql.yml \
  --vault-password-file .vault-password \
  -e tuki_confirm_sql_restore=true
```

Restore safety is strict:

- the acknowledgement is mandatory;
- the transferred backup must be non-empty and checksum-verified;
- SQL Server must be running and healthy;
- `RESTORE FILELISTONLY` determines logical data and log names;
- exactly one data file and one log file are required for this automated path;
- an existing target database or target MDF/LDF causes failure;
- restore uses explicit `MOVE` paths;
- `WITH REPLACE` is never used;
- the restored database must be `ONLINE` and pass a basic query.

The restored files are placed at:

```text
/var/opt/mssql/data/<database>.mdf
/var/opt/mssql/data/<database>_log.ldf
```

### 6. Migrate Pelias shared data and index

First migrate Pelias shared data. The source defaults to the Azure Central
Luzon project and includes `data/` plus `blacklist/`, while pruning
`data/elasticsearch` from the archive:

```bash
ansible-playbook -i inventory.local.ini playbooks/transfer-pelias-data.yml \
  --vault-password-file .vault-password
```

The Azure/controller/GCP archive checksums and recursive file count/byte totals
must match. If GCP already has non-empty `data/` or `blacklist/`, the playbook
fails. A deliberate replacement requires:

```bash
-e tuki_confirm_pelias_data_replace=true
```

The previous GCP directories are renamed with a timestamp and retained for
rollback; they are not deleted.

Then migrate the `pelias` index logically:

```bash
ansible-playbook -i inventory.local.ini playbooks/transfer-pelias-index.yml \
  --vault-password-file .vault-password
```

The playbook discovers Elasticsearch through Compose where possible, verifies
the Azure source count dynamically, exports settings/mappings/documents with a
digest-pinned elasticdump image, transfers the logical archive through a secure
controller temporary directory, and requires the GCP count to match. The
currently observed Azure count (142,957) is not hardcoded. If the GCP `pelias`
index already exists, migration fails unless the operator supplies:

```bash
-e tuki_confirm_pelias_index_replace=true
```

With that acknowledgement, the existing GCP index is first exported to
`/opt/tuki/backups/pelias-index/previous-<timestamp>/` before it is replaced.
Elasticsearch and all Pelias support ports remain private to `tuki-internal`.

For a legacy Azure Pelias Elasticsearch container outside this Compose project,
set the fallback locally (the known Azure default is already represented in
`group_vars/all.yml`):

```bash
-e tuki_pelias_es_container_fallback=pelias_elasticsearch
```

### 7. Validate all migrated data

Before deploying applications, validate SQL row counts and application-critical
queries manually. Also validate the Pelias index and Valhalla tiles/routes. The
deployment acknowledgement means an operator has completed these checks.

### 8. Deploy applications

```bash
ansible-playbook -i inventory.local.ini playbooks/deploy.yml \
  --vault-password-file .vault-password \
  -e tuki_confirm_data_ready=true
```

This requires healthy SQL Server, Elasticsearch, and Valhalla plus a non-empty
Pelias index. It then requires healthy libpostal, placeholder, interpolation,
and Pelias API before starting the backend, admin, and Caddy.

### 9. Verify

```bash
ansible-playbook -i inventory.local.ini playbooks/verify.yml \
  --vault-password-file .vault-password
```

Verification checks all ten service health states, Elasticsearch index health,
a non-zero Pelias document count, a real `SM City Clark, Mabalacat, Pampanga`
search with at least one feature, and both public HTTPS endpoints with
certificate validation.

## Rehearsal

Use dedicated rehearsal Azure/GCP hosts or isolated copies of the persistent
data directories. Point `inventory.local.ini` at those hosts, review all paths,
and execute the same sequence above. Never rehearse a restore against a GCP SQL
Server that already holds the production database; the playbook will refuse it
in any case. Syntax-only validation is safe on the controller:

```bash
for playbook in playbooks/*.yml; do
  ansible-playbook -i inventory.local.ini "$playbook" \
    --syntax-check --vault-password-file .vault-password
done
```

## Final production cutover

1. Stop or disable production writes on Azure.
2. Create a fresh final Azure backup (explicitly archive the earlier rehearsal
   backup if needed).
3. Transfer it through the controller and verify all checksums.
4. Restore it on GCP with the explicit acknowledgement.
5. Transfer Pelias shared data and logically migrate its index; verify dynamic
   source/destination counts and support-service health.
6. Validate SQL data, Pelias searches, Valhalla, and application-critical queries.
7. Run `deploy.yml`, then `verify.yml`.
8. Switch public DNS to GCP only after verification passes.
9. Keep Azure intact and unavailable for writes temporarily as a rollback
   source until the cutover is accepted.


## Automated Google Drive database backups

The `backups.yml` playbook installs rclone, renders the encrypted Google Drive
configuration to `/etc/rclone/rclone.conf` with mode `0600`, installs the
database backup script, and enables systemd timers for daily, weekly, and
monthly SQL Server backups.

Keep the complete working rclone remote in encrypted `group_vars/vault.yml`:

```yaml
tuki_rclone_config: |
  [gdrive]
  type = drive
  ...
```

The default retention policy is 7 daily, 4 weekly, and 3 monthly backups under:

```text
gdrive:Tuki/production/database/
├── daily/
├── weekly/
└── monthly/
```

Migration snapshots remain separate from this automatic retention policy.

Install or update the backup machinery on one host:

```bash
ansible-playbook -i inventory.local.ini playbooks/backups.yml \
  --limit azure \
  --vault-password-file .vault-password
```

Use `--limit gcp` after migration. The same playbook can manage both hosts,
but normally only the active production database host should have its timers
enabled to avoid two independent systems writing production backup sets.

The default schedules are evaluated in `Asia/Manila` regardless of VM
timezone: daily at 02:00, Sunday at 03:00, and the first day of each month at
04:00. Each timer has up to five minutes of randomized delay. Change the
`tuki_backup_*_calendar` variables in `group_vars/all.yml` if needed.

Inspect the timers:

```bash
sudo systemctl list-timers 'tuki-db-backup-*.timer'
```

Run a daily backup immediately for validation:

```bash
sudo systemctl start tuki-db-backup@daily.service
sudo systemctl status tuki-db-backup@daily.service
sudo journalctl -u tuki-db-backup@daily.service --no-pager
```

Each run resolves the SQL Server container, creates a compressed SQL Server
backup with page checksums, copies it to a restricted host staging directory,
uploads it with rclone, compares the remote and local byte counts, prunes only
the oldest files in that retention tier, and removes the local copy only after
the upload verifies successfully. A failed upload leaves the completed local
`.bak` available for operator recovery. A lock prevents overlapping daily,
weekly, and monthly jobs.


## Staging database

The staging database is provisioned by Ansible rather than manually. The
default database name is `Tuki_Staging`.

Run from `infra/ansible`:

```bash
ansible-playbook -i inventory.local.ini playbooks/prepare-staging-db.yml \
  --limit azure
```

The playbook:

```text
resolve SQL Server container
→ create Tuki_Staging only when missing
→ copy TukiDbSchema.sql
→ copy TukiNavigationSchema.sql
→ copy every database/migrations/*.sql in filename order
→ apply the full additive schema chain
→ verify critical tables exist
```

It reads the SQL Server SA password only from the running SQL Server container
and passes it to `sqlcmd` through `SQLCMDPASSWORD`; no SQL password is
required on the command line or stored in this playbook.

Azure currently uses the host-specific
`group_vars/tuki_azure.yml` fallback `tuki-sql` for its legacy standalone
SQL Server container. GCP and future Compose-managed hosts continue to prefer
Compose service discovery.

Because the schema scripts are additive/idempotent, rerunning the playbook is
the normal way to bring `Tuki_Staging` up to the repository's current schema.
It does not drop or recreate the staging database.

### Staging reference data

Prepare the staging schema before synchronizing transportation reference data,
then deploy staging services in a later, separate phase:

```text
prepare-staging-db.yml
    ↓
sync-staging-reference-data.yml
    ↓
deploy staging services
```

The reference synchronization copies only these approved tables from `Tuki`
to `Tuki_Staging`, in foreign-key-safe order:

```text
TransportModes
TransportStops
TransportRoutes
RoutePoints
RouteWaypoints
RouteStops
RouteSegments
FareRules
TricyclePoints
TransferConnections
```

Run the synchronization from `infra/ansible` with its explicit confirmation:

```bash
ansible-playbook -i inventory.local.ini \
  playbooks/sync-staging-reference-data.yml \
  --limit azure \
  -e tuki_confirm_staging_reference_sync=true
```

The playbook clears and repopulates only those approved tables in
`Tuki_Staging`. It never writes to `Tuki`, refuses equal source and destination
database names, preserves identity values and routing relationships, and wraps
the destination refresh and verification in one transaction. Any SQL or count
verification failure rolls back the staging changes. It also fails unless the
staging user, API-key, passenger-trip, trip-session, chat-conversation, and
chat-message tables remain empty.

## Staging continuous deployment

The staging release path is intentionally separate from production:

```text
feature branch
    ↓ pull request
dev
    ↓ CI
staging
    ↓ CI (.NET, Compose, database)
staging CD
    ↓
prepare-staging-db.yml
    ↓
sync-staging-reference-data.yml
    ↓
deploy-staging.yml
    ↓
staging-api.tuki.ph + staging-admin.tuki.ph
    ↓ manual validation
pull request from staging to main
```

The CI workflow calls `.github/workflows/deploy-staging.yml` only for a push to
the `staging` branch and only after all three CI jobs succeed. Pull requests,
manual CI runs, `dev`, `main`, and arbitrary feature branches cannot invoke the
deployment job. The called workflow checks out and deploys the exact commit SHA
tested by that CI run. Staging runs are serialized instead of canceling an
in-progress deployment.

Create and protect the GitHub Actions `staging` environment used by the deploy
job. Configure these as that environment's secrets (repository or organization
secrets inherited by the caller are also supported):

- `AZURE_HOST`
- `AZURE_SSH_USER`
- `AZURE_SSH_PRIVATE_KEY`
- `AZURE_SSH_HOST_KEY` (a trusted complete `known_hosts` entry)
- `ANSIBLE_VAULT_PASSWORD`

Both `staging-api.tuki.ph` and `staging-admin.tuki.ph` must resolve to the
`AZURE_HOST` address before deployment. CD checks this before running Ansible,
and the final HTTPS checks keep certificate verification enabled. Correct DNS
and allow time for propagation before the first staging deployment.

The staging application uses a separate checkout at `/opt/tuki/staging/AUP`
and the dedicated `docker-compose.staging.yml` project. Only
`backend-staging` and `admin-staging` belong to that project. Their host ports
are bound to loopback (`127.0.0.1:5130` and `127.0.0.1:5031`), so only the
host-managed Caddy service exposes them through HTTPS. No staging SQL Server,
Pelias, Elasticsearch, or Valhalla container is created.

The existing SQL Server, Pelias API, and Valhalla containers are connected to
the private `tuki-staging-internal` bridge with staging-specific aliases. This
does not restart or replace them. Azure uses the legacy names in
`group_vars/tuki_azure.yml`; future Compose-managed data services are resolved
by service name first. Production Compose service definitions and containers
are not lifecycle-managed by `deploy-staging.yml`, and it never runs
`docker compose down`.

Staging environment files are rendered mode `0600` from the existing encrypted
Vault values. The backend template hard-codes the configured staging database
name (`Tuki_Staging`) while retaining Pelias, Valhalla, Google, Gemini, email,
authentication, and admin-login settings. The playbook reads the rendered file
under `no_log` and refuses deployment if it targets `Tuki`.

The backend `/health` endpoint checks application liveness, SQL connectivity,
and a real `TransportRoutes` query. It does not call Gemini, Google, email, or
other paid/external APIs. The admin has a liveness-only `/health` endpoint. Both
container health checks use these endpoints. Caddy configuration is validated
as a candidate before the managed staging fragment is installed and the host
Caddy service is safely reloaded; existing production site blocks remain
unchanged.

### Manual staging commands

Run from `infra/ansible`. The full deploy requires the encrypted Vault password
and an exact commit from the remote `staging` branch.

Prepare or update the staging schema:

```bash
ansible-playbook -i inventory.local.ini playbooks/prepare-staging-db.yml \
  --limit azure
```

Refresh approved reference data:

```bash
ansible-playbook -i inventory.local.ini \
  playbooks/sync-staging-reference-data.yml \
  --limit azure \
  -e tuki_confirm_staging_reference_sync=true
```

Run the complete staging deployment for the checked-out staging commit:

```bash
STAGING_COMMIT="$(git rev-parse HEAD)"
ansible-playbook -i inventory.local.ini playbooks/deploy-staging.yml \
  --limit azure \
  --vault-password-file .vault-password \
  -e tuki_confirm_staging_reference_sync=true \
  -e "tuki_staging_git_revision=$STAGING_COMMIT" \
  -e "tuki_staging_image_tag=$STAGING_COMMIT"
```

Verify public application, database, and routing health:

```bash
curl --fail --silent --show-error https://staging-api.tuki.ph/health
curl --fail --silent --show-error https://staging-admin.tuki.ph/health
```

Inspect only the isolated staging containers:

```bash
ansible -i inventory.local.ini azure -b -m shell -a \
  'cd /opt/tuki/staging/AUP && docker compose --env-file runtime/staging-compose.env -f docker-compose.staging.yml ps'
```

Inspect staging logs without touching production containers:

```bash
ansible -i inventory.local.ini azure -b -m shell -a \
  'cd /opt/tuki/staging/AUP && docker compose --env-file runtime/staging-compose.env -f docker-compose.staging.yml logs --tail 200 backend-staging admin-staging'
```

The reference-data task prints source and staging counts for all ten approved
tables and prints zero counts for the six protected sensitive tables. Any
mismatch, sensitive row, SQL error, container health failure, Caddy validation
error, TLS error, or non-2xx health response fails the deployment visibly.
