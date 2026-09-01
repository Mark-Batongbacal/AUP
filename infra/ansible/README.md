# Tuki production deployment

The bootstrap playbook targets the existing `tuki-gcp` inventory group on
Ubuntu 24.04. It installs Docker Engine and the Compose plugin, keeps host SSH
enabled, creates `/opt/tuki` and persistent service directories, updates the
`dev` branch in `/opt/tuki/AUP`, renders service-specific environment files,
and runs
`docker compose up -d --build`.

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
They live in `/opt/tuki/AUP/runtime`, which is Git-ignored and mode `0700`;
each file is mode `0600`. Docker Compose does not consume the root
`.env.example`; that file is placeholder-only documentation.

## Manage production secrets

`group_vars/vault.yml` is committed only in Ansible Vault-encrypted form. Edit
it from `infra/ansible` with:

```bash
ansible-vault edit group_vars/vault.yml
```

Store the Vault password locally in `.vault-password` with mode `0600`, or use
another secure Ansible-supported password source. Never commit the Vault
password, decrypted Vault content, or rendered runtime files.

## Configure and deploy

1. Review `inventory.ini` and its SSH key path.
2. Copy non-secret values from `group_vars/all.example.yml` into
   `group_vars/all.yml`, then edit the encrypted Vault as described above.
3. Keep `.vault-password` outside Git and transfer it through your password
   manager.
4. Point the API and admin DNS records to the VM. In the GCP firewall allow TCP
   22, 80, and 443, plus UDP 443 if HTTP/3 is desired. Do not expose 1433,
   4000, 5030, 5129, or 8002.
5. From `infra/ansible`, run:

   ```bash
   ansible-playbook playbooks/bootstrap.yml --vault-password-file .vault-password
   ```

Before starting containers, the playbook validates required Vault variables,
renders the runtime files without logging decrypted content, and runs
`docker compose config --quiet`. The Git update uses `force: false`, so the
playbook does not discard changes on the VM. SSH remains on the host at port
22; neither Compose nor Caddy manages it.

## Separate data migration workflow

Database backup and restore are deliberately absent from `bootstrap.yml`.
Deploying the stack never creates, restores, replaces, or deletes the `Tuki`
database.

Migrate production data as a separate, operator-controlled operation:

1. Stop writes and create a verified SQL Server backup at the source.
2. Transfer the backup to a restricted staging directory on the VM.
3. Restore it manually into `sqlserver` without `WITH REPLACE`; abort if the
   target database already exists.
4. Review and apply any additive scripts under `database/` only after another
   backup.
5. Validate the restored database before restarting `backend` and `admin`.

Valhalla tiles and the Pelias Elasticsearch index must likewise be migrated or
built in an explicit data preparation step. Compose persists those directories
but does not overwrite their contents.
