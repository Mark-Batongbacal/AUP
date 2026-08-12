# Backend local configuration

Copy `appsettings.Development.json.example` to `appsettings.Development.json` for non-secret development settings. Put local credentials in the ignored `.env` file; the application loads it automatically when `ASPNETCORE_ENVIRONMENT=Development`.

```bash
export Login__Users__0__UserName='admin@aup.edu'
export Login__Users__0__Password='your-login-password'
export NVIDIA_API_KEY='your-nvidia-api-key'
```

Both `.env` and `appsettings.Development.json` are intentionally ignored by Git. Never put API keys, passwords, or issued API keys in `.http` request files.

## Deploying to Render

Create a **Web Service** from this repository and leave the Root Directory empty. Render detects the repository-root Dockerfile and builds the `backend` project. Add these environment variables in the Render dashboard; do not put their real values in `appsettings.json`:

```text
Login__Users__0__UserName=admin@aup.edu
Login__Users__0__Password=<a-long-unique-password>
Login__Users__1__UserName=<second-user-name>
Login__Users__1__Password=<another-long-unique-password>
Login__Users__2__UserName=<third-user-name>
Login__Users__2__Password=<another-long-unique-password>
Login__Users__3__UserName=<fourth-user-name>
Login__Users__3__Password=<another-long-unique-password>
NVIDIA_API_KEY=<your-nvidia-api-key>
```

Render provides `PORT` automatically. The application listens on that port inside the container.

The Android app does not need a CORS environment variable. CORS applies only to browser-based clients.
