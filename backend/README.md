# Backend local configuration

Copy `appsettings.Development.json.example` to `appsettings.Development.json` for non-secret development settings. Put local credentials in the ignored `.env` file; the application loads it automatically when `ASPNETCORE_ENVIRONMENT=Development`.

```bash
export Login__Users__0__UserName='admin@aup.edu'
export Login__Users__0__Password='your-login-password'
export NVIDIA_API_KEY='your-nvidia-api-key'
export ConnectionStrings__TukiDbConnection='<sql-server-connection-string>'
export Valhalla__BaseUrl='https://your-valhalla-instance.example.com'
export Facebook__AppId='<facebook-app-id>'
export Facebook__AppSecret='<facebook-app-secret>'
export Pelias__BaseUrl='http://your-pelias-instance.example.com:4000'
```

Both `.env` and `appsettings.Development.json` are intentionally ignored by Git. Never put API keys, passwords, database connection strings, or issued API keys in `.http` request files.

## SQL Server database with EF Core

The backend connects to the existing SQL Server `TukiDb` database through EF Core using `TukiDbContext`. The tables already exist in `dbo`; do not run migrations or `dotnet ef database update` unless a future task explicitly asks for schema changes.

The reproducible additive schema script is tracked at `../database/TukiDbSchema.sql`; see `../database/README.md` for the SSMS workflow. The active model layer keeps API-friendly property names where possible and maps them explicitly to the existing PascalCase SQL Server tables and columns in `Models/Database/TukiDbContext.cs`.

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
ConnectionStrings__TukiDbConnection=<sql-server-connection-string>
Facebook__AppId=<facebook-app-id>
Facebook__AppSecret=<facebook-app-secret>
Pelias__BaseUrl=<your-pelias-base-url>
```

Render provides `PORT` automatically. The application listens on that port inside the container.

The Android app does not need a CORS environment variable. CORS applies only to browser-based clients.
