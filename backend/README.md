# Backend local configuration

Copy `appsettings.Development.json.example` to `appsettings.Development.json` and keep the copy local, or provide the values as environment variables before starting the API:

```bash
export Login__InitialUserName='your-login-name'
export Login__InitialPassword='your-login-password'
```

`appsettings.Development.json` is intentionally ignored by Git. Never put API keys, passwords, or issued API keys in `.http` request files.
