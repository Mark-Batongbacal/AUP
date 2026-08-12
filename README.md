# AUP

Android frontend and ASP.NET backend for the AUP project.

## Project layout

- `frontend/` — Android app built with Jetpack Compose.
- `backend/` — ASP.NET Core API.
- `Dockerfile` — Render container build for the backend; it must remain at the repository root.

## Prerequisites

- Android Studio, Android SDK Platform 36, and JDK 17 or later for the Android app.
- .NET 9 SDK for the backend.
- Git for source control.
- GitHub CLI (`gh`) if you want to authenticate and work with GitHub from the terminal.

## Run locally

### Android frontend

Open `frontend/` in Android Studio, select an emulator or physical Android device, then run the `app` configuration. You can also build it from Windows PowerShell:

```powershell
cd frontend
.\gradlew.bat :app:assembleDebug
```

### Backend

The backend loads local credentials from `backend/.env` when `ASPNETCORE_ENVIRONMENT=Development`. The file is ignored by Git.

Create `backend/.env` with your own values:

```text
Login__Users__0__UserName=admin@aup.edu
Login__Users__0__Password=<secure-password>
Login__Users__1__UserName=<second-user-name>
Login__Users__1__Password=<secure-password>
Login__Users__2__UserName=<third-user-name>
Login__Users__2__Password=<secure-password>
Login__Users__3__UserName=<fourth-user-name>
Login__Users__3__Password=<secure-password>
NVIDIA_API_KEY=<your-nvidia-api-key>
ConnectionStrings__Supabase=Host=<supabase-host>;Port=5432;Database=postgres;Username=postgres;Password=<database-password>
```

Never commit real passwords or API keys. `appsettings.json` contains only non-secret defaults; `appsettings.Development.json` and `.env` are ignored.

Start the backend in Windows PowerShell:

```powershell
cd backend
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

On macOS/Linux, use `ASPNETCORE_ENVIRONMENT=Development dotnet run` instead.

The local API runs at `http://localhost:5129` by default. Use `backend/backend.http` to test login, authenticated API calls, and the NVIDIA NIM request.

The public health endpoint requires no API key:

```text
GET /health
```

It returns a `200 OK` response with `status` and the server-side response time in milliseconds.

## Set up Git and GitHub CLI

Install Git on Windows (open PowerShell as a normal user):

```powershell
winget install --id Git.Git -e --source winget
```

Configure the identity used for your commits:

```powershell
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```

Install GitHub CLI on Windows, then authenticate:

```powershell
winget install --id GitHub.cli -e --source winget
gh auth login
```

Restart PowerShell if either command is not found. In `gh auth login`, choose `GitHub.com`, then use the browser login flow and select HTTPS or SSH according to your preferred Git setup. Confirm authentication with:

```powershell
gh auth status
```

Clone the project if you do not already have it:

```powershell
git clone <repository-url>
cd AUP
```

## Team branch workflow

Before starting work, update `dev`, switch to your own branch, and merge the current development changes:

```powershell
# 1. Update dev
git switch dev
git pull

# 2. Switch to your branch
git switch <your-branch>

# 3. Bring in the latest dev changes
git merge dev

# 4. Start coding
```

For a new branch:

```powershell
git switch dev
git pull
git switch -c <your-branch>
```

Before committing, check what will be included and ensure `.env` is not staged:

```powershell
git status
git add <files-you-changed>
git commit -m "Describe your change"
git push -u origin <your-branch>
```
See the [frontend development guide](frontend/README.md) for instructions on
running the app alongside the local backend.
