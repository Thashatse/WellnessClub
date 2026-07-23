# WellnessClub

A points system for a bi-weekly employee wellness club leaderboard, built on Strava activity data.

## How it's put together

- **`WellnessClub.Shared`** — models, the Strava API client (OAuth exchange/refresh, activity fetch, rate limiting), period/points calculation, and the sync engine. Used by both projects below so they can never drift apart on scoring.
- **`WellnessClub.Api`** — an ASP.NET Core app with two parts:
  - `/auth/strava/...` — the public consent flow employees use to connect their Strava account.
  - `/dashboard/...` — a password-gated admin dashboard for generating leaderboard reports, tuning scoring rules, and viewing connected employees.
- **`WellnessClub.Sync`** — a console tool that runs the same sync from a terminal and prints/saves a Markdown report.
- **`infra/`** — a Bicep template that deploys the Api to Azure on the App Service free (F1) tier.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An Azure Storage Account (used for Table Storage — an `Athletes` table and a `ClubConfig` table, both created automatically on first run)
- A Strava API application — create one at https://www.strava.com/settings/api. You'll need its Client ID and Client Secret, and for local development set its **Authorization Callback Domain** to `localhost`
- The Strava Club ID for the club members should belong to (the numeric ID in the club's Strava URL)

## Branding assets (not included in this repo)

`WellnessClub.Api/wwwroot/` is gitignored — it holds this org's own logo and favicons, which aren't meant to be reused elsewhere. After cloning, create the folder and add your own:

| File | Size | Used for |
|---|---|---|
| `WellnessClub.Api/wwwroot/mascot.png` | 96×96 | Logo shown on every auth and dashboard page |
| `WellnessClub.Api/wwwroot/favicon-32.png` | 32×32 | Browser tab icon |
| `WellnessClub.Api/wwwroot/apple-touch-icon.png` | 180×180 | iOS/Android home-screen icon |

The app runs fine without these — you'll just get broken image icons and a missing favicon until they're added.

## Setup

### 1. Configure `WellnessClub.Api`

Create `WellnessClub.Api/appsettings.Development.json` (gitignored) with your real values:

```json
{
  "Strava": {
    "ClientId": "<your-strava-client-id>",
    "ClientSecret": "<your-strava-client-secret>",
    "RedirectUri": "http://localhost:5169/auth/strava/callback",
    "ClubId": "<your-strava-club-id>"
  },
  "Azure": {
    "StorageConnection": "<your-storage-account-connection-string>"
  },
  "Dashboard": {
    "Password": "<choose-a-password>"
  }
}
```

Also update `CompanyName` in `WellnessClub.Api/appsettings.json` — it ships set to this org's name and is shown throughout the dashboard, auth pages, and reports.

### 2. Configure `WellnessClub.Sync`

Copy `WellnessClub.Sync/sync-config.example.json` to `WellnessClub.Sync/sync-config.json` (gitignored) and fill in the same Storage connection string, Strava credentials, and your company name:

```json
{
  "StorageConnection": "<your-storage-account-connection-string>",
  "StravaClientId": "<your-strava-client-id>",
  "StravaClientSecret": "<your-strava-client-secret>",
  "CompanyName": "<Your Company Name>"
}
```

### 3. Seed the shared scoring/period config

The bi-weekly cycle boundaries and point values are stored once in the `ClubConfig` table so the Api and the console tool always agree. Copy `WellnessClub.Sync/club-config.example.json` to `WellnessClub.Sync/club-config.json` (gitignored):

```json
{
  "CycleAnchor": "2026-01-09",
  "CycleLengthDays": 14,
  "ClubId": "<your-strava-club-id>",
  "MaxConcurrency": 8,
  "Points": {
    "PrBonus": 1,
    "PeriodTotalBonus": 2,
    "GroupBonus": 1,
    "RacePoints": 7
  }
}
```

`CycleAnchor` is a Friday that starts one of your bi-weekly cycles — pick one and every cycle boundary is computed from it. Then seed it into Table Storage:

```
cd WellnessClub.Sync
dotnet run -- seed-club-config
```

(Scoring values can also be changed later from the dashboard's Settings page — that's the same underlying config row.)

### 4. Run it

```
dotnet run --project WellnessClub.Api    # http://localhost:5169 — /auth/strava to test the connect flow, /dashboard/login for the admin dashboard
dotnet run --project WellnessClub.Sync   # syncs the current period from the CLI and prints/saves a Markdown report
```

## Deploying to Azure (optional)

Copy `infra/main.bicepparam.example` to `infra/main.bicepparam` (gitignored), fill in your values, then:

```
az deployment group create --resource-group <your-resource-group> --parameters infra/main.bicepparam
```

This provisions a Storage Account and an App Service on the free F1 plan — no other Azure resources are needed, and at this scale the whole thing runs at effectively zero cost.
