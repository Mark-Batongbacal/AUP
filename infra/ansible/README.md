# Tuki production deployment

Tuki deployment is split into four Ansible phases so host preparation, data
services, application deployment, and verification cannot accidentally collapse
into one destructive operation.

```text
bootstrap.yml
    ↓
prepare-services.yml
    ↓
operator-controlled data migration / build
    ↓
deploy.yml
    ↓
verify.yml
```

The playbooks target the existing `tuki_gcp` inventory group on Ubuntu 24.04.

## Secret flow

Secrets flow through deployment as follows:

```text
Ansible Vault
    ↓ decrypt during deployment
Ansible templates
    ↓
runtime/*.env on server (0600)
    ↓
Docker Compose env_file
    ↓
containers
```

The generated files are scoped by service: `backend.env` contains backend
configuration and credentials, `sqlserver.env` contains only SQL Server
settings, `admin.env` contains only the admin application's runtime settings,
and `caddy.env` contains only public hostnames and the ACME email address.
`compose.env` contains non-secret Compose interpolation values such as the Tuki
image tag, persistent data root, Valhalla tile URL, and Pelias Elasticsearch
JVM options. They live in `/opt/tuki/AUP/runtime`, which is Git-ignored and mode
`0700`; each file is mode `0600`.

Any manual Compose command that must honor the Ansible-managed interpolation
values should use:

```bash
docker compose --env-file runtime/compose.env <command>
```

## Manage production secrets

`group_vars/vault.yml` is committed only in Ansible Vault-encrypted form. Edit
it from `infra/ansible` with:

```bash
ansible-vault edit group_vars/vault.yml --vault-password-file .vault-password
```

Store the Vault password locally in `.vault-password` with mode `0600`, or use
another secure Ansible-supported password source. Never commit the Vault
password, decrypted Vault content, private SSH keys, or rendered runtime files.

## 1. Bootstrap the GCP host

Before running Ansible:

1. Review `inventory.ini` and its SSH key path on the actual Ansible control
   machine. The private key is intentionally not stored in Git.
2. With host-key checking enabled, connect to the VM with SSH once and verify
   its host key so the VM is present in `known_hosts`.
3. Review non-secret values in `group_vars/all.yml` and edit the encrypted
   Vault as described above.
4. Point the API and admin DNS records to the VM. In the GCP firewall allow TCP
   22, 80, and 443, plus UDP 443 if HTTP/3 is desired. Do not expose 1433,
   4000, 5030, 5129, 8002, 9200, or 9300.

Run:

```bash
ansible-playbook playbooks/bootstrap.yml --vault-password-file .vault-password
```

`bootstrap.yml` installs Docker Engine and the Compose plugin, keeps host SSH
enabled, creates `/opt/tuki` and persistent service directories, updates the
`dev` branch in `/opt/tuki/AUP`, renders runtime configuration, and validates:

```bash
docker compose --env-file runtime/compose.env config --quiet
```

It deliberately starts no containers.

## 2. Prepare data-service containers

Run:

```bash
ansible-playbook playbooks/prepare-services.yml --vault-password-file .vault-password
```

This starts only the data-service layer:

```text
sqlserver
pelias-elasticsearch
pelias
valhalla
```

The playbook waits for SQL Server and Elasticsearch before starting Pelias.
Valhalla may continue building tiles in the background because a first build can
be lengthy. This phase does not restore a SQL backup, import a Pelias index, or
replace Valhalla data.

## 3. Migrate or build data explicitly

Database backup and restore are deliberately absent from the deployment
playbooks. Ansible must not silently initialize, replace, or delete production
data.

For SQL Server migration:

1. Stop writes at the source when performing the final cutover.
2. Create and verify a SQL Server `.bak` backup.
3. Transfer the backup to a restricted staging location on the target VM.
4. Restore into the target `sqlserver` container without `WITH REPLACE`; abort
   if an unexpected target database already exists.
5. Validate the restored database before application deployment.

Valhalla tiles and the Pelias Elasticsearch index must likewise be migrated or
built deliberately. Stop the affected service before replacing files underneath
its persistent data directory.

## 4. Deploy backend, admin, and Caddy

After the SQL Server database, Pelias data, and Valhalla data have been
validated, run:

```bash
ansible-playbook playbooks/deploy.yml \
  --vault-password-file .vault-password \
  -e tuki_confirm_data_ready=true
```

The explicit `tuki_confirm_data_ready=true` acknowledgement is required to
prevent an accidental application deployment against empty or incomplete data.

Before starting application services, `deploy.yml` requires these services to
already be healthy:

```text
sqlserver
pelias-elasticsearch
pelias
valhalla
```

It then starts the application layer in order:

```text
backend → admin → caddy
```

Each service is started with `--no-deps`, so this playbook cannot silently
create or restart missing data-service dependencies. The backend and admin
images are built from the checked-out repository; Caddy remains the only public
application entry point.

## 5. Verify the deployment

Run:

```bash
ansible-playbook playbooks/verify.yml --vault-password-file .vault-password
```

`verify.yml` checks that all seven Compose services report healthy, then tests
the public API and admin HTTPS endpoints using normal certificate validation.
A public endpoint may legitimately return an application-level 4xx response at
`/`; verification fails on TLS/connectivity failures and HTTP 5xx responses.

The expected final request path is:

```text
Internet
   ↓ HTTPS
Caddy :443
   ├── api hostname   → backend:5129
   └── admin hostname → admin:5030

Internal Docker network only:
   sqlserver:1433
   valhalla:8002
   pelias:4000
   pelias-elasticsearch:9200/9300
```

## Re-running playbooks

`bootstrap.yml` is safe to use for host/configuration convergence and does not
start services. `prepare-services.yml` can re-run the data-service `up -d`
operations without replacing persistent data. `deploy.yml` always requires the
explicit data-readiness acknowledgement. Data restore/import operations remain
manual or must be implemented as separate operator-controlled migration
playbooks with their own safeguards.
