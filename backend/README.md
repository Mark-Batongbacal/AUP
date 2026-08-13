# Backend local configuration

Copy `appsettings.Development.json.example` to `appsettings.Development.json` for non-secret development settings. Put local credentials in the ignored `.env` file; the application loads it automatically when `ASPNETCORE_ENVIRONMENT=Development`.

```bash
export Login__Users__0__UserName='admin@aup.edu'
export Login__Users__0__Password='your-login-password'
export NVIDIA_API_KEY='your-nvidia-api-key'
export ConnectionStrings__Supabase='Host=your-host;Port=5432;Database=postgres;Username=postgres;Password=your-password'
```

Both `.env` and `appsettings.Development.json` are intentionally ignored by Git. Never put API keys, passwords, or issued API keys in `.http` request files.

## Supabase database with EF Core

The backend connects to Supabase as a PostgreSQL database through EF Core. Copy the database connection string from the Supabase **Connect** panel into `ConnectionStrings__Supabase`; this is separate from a Supabase API key.

To generate EF Core models from existing Supabase tables, install the EF CLI once and run this command from `backend/`:

```bash
dotnet tool install --global dotnet-ef
ASPNETCORE_ENVIRONMENT=Development dotnet ef dbcontext scaffold "Name=ConnectionStrings:Supabase" Npgsql.EntityFrameworkCore.PostgreSQL --schema public --context SupabaseDbContext --context-dir Models/Database --output-dir Models/Database --no-onconfiguring --use-database-names --force
```

The command creates the entity classes and `SupabaseDbContext` under `Models/Database/`. Query the resulting `DbSet` properties with normal LINQ, for example:

```csharp
var students = await database.Students
    .Where(student => student.Course == "BSIT")
    .OrderBy(student => student.Name)
    .ToListAsync(cancellationToken);
```

Rerun the scaffold command when the Supabase schema changes. Generated models can be overwritten, so keep custom query logic in services or partial classes rather than editing generated files directly.
The current C# model layer uses PascalCase entity and property names with explicit EF Core mapping back to Supabase's snake_case tables and columns. Future database-first scaffolding may regenerate database-style names and should be reviewed carefully before replacing these files.

Supabase's PostGIS extension is installed in the `gis` schema. The spatial columns that EF cannot
scaffold are mapped manually in `Models/Database/SpatialProperties.cs` as NetTopologySuite `Point`
and `LineString` properties. Keep that file when re-scaffolding.

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
