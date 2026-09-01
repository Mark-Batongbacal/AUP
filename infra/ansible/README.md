# Tuki production deployment

The bootstrap playbook targets the existing `tuki_gcp` inventory group on
Ubuntu 24.04. It installs Docker Engine and the Compose plugin, keeps host SSH
enabled, creates `/opt/tuki` and persistent service directories, updates the
`dev` branch in `/opt/tuki/AUP`, renders service-specific environment files,
renders Docker Compose interpolation variables, and validates the Compose
configuration. It deliberately does not start application or data-service
containers.

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
`0700`; each file is mode `0600`. Docker Compose does not consume the root
`.env.example`; that file is placeholder-only documentation.

Any Compose command that must honor the Ansible-managed interpolation values
should use:

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
password, decrypted Vault content, or rendered runtime files.

## Configure and bootstrap

1. Review `inventory.ini` and its SSH key path on the actual Ansible control
   machine. The private key is intentionally not stored in Git.
2. With host-key checking enabled, connect to the VM with SSH once and verify
   its host key before running Ansible so the host is present in `known_hosts`.
3. Review non-secret values in `group_vars/all.yml`, then edit the encrypted
   Vault as described above.
4. Keep `.vault-password` outside Git and transfer it through your password
   manager.
5. Point the API and admin DNS records to the VM. In the GCP firewall allow TCP
   22, 80, and 443, plus UDP 443 if HTTP/3 is desired. Do not expose 1433,
   4000, 5030, 5129, 8002, 9200, or 9300.
6. From `infra/ansible`, run:

   ```bash
   ansible-playbook playbooks/bootstrap.yml --vault-password-file .vault-password
   ```

The playbook validates required variables, renders runtime files without
logging decrypted content, and runs:

```bash
docker compose --env-file runtime/compose.env config --quiet
```

The Git update uses `force: false`, so the playbook does not discard changes on
the VM. SSH remains on the host at port 22; neither Compose nor Caddy manages
it.

## Deployment remains a separate phase

`bootstrap.yml` is host preparation only. A deployment playbook should start
services only after the required SQL Server, Valhalla, and Pelias data has been
prepared or migrated. Keeping this separate prevents a fresh VM bootstrap from
accidentally bringing up an empty production database or incomplete routing and
search services.

## Separate data migration workflow

Database backup and restore are deliberately absent from `bootstrap.yml`.
Bootstrapping the host never creates, restores, replaces, or deletes the `Tuki`
database.

Migrate production data as a separate, operator-controlled operation:

1. Stop writes and create a verified SQL Server backup at the source.
2. Transfer the backup to a restricted staging directory on the VM.
3. Restore it manually into `sqlserver` without `WITH REPLACE`; abort if the
   target database already exists.
4. Review and apply any additive scripts under `database/` only after another
   backup.
5. Validate the restored database before starting `backend` and `admin`.

Valhalla tiles and the Pelias Elasticsearch index must likewise be migrated or
built in an explicit data preparation step. Compose persists those directories
but does not overwrite their contents.
