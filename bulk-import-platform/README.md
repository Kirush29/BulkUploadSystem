# Customer and Vehicle Bulk Data Import Platform

A full-stack implementation of the bulk import platform described in the Project Proposal:
upload, validate, transform, review, and import Vehicle and Customer data through a
controlled 9-step workflow (Upload → Parse → Field Mapping → Validation → Default Values →
Preview/Review → Bulk Import → Result Generation → Audit Recording).

## Stack

- **Backend**: ASP.NET Core Web API (.NET 8), EF Core, SQL Server, ClosedXML — matches the
  proposal's System Architecture (section 11) exactly.
- **Frontend**: React + TypeScript + Material UI (MUI), built with Vite.

## ⚠️ Important: backend was written but not compiled/run in the sandbox

This project was built in a sandboxed environment that has access to the npm registry but
**not** NuGet (api.nuget.org). That means:

- The **frontend** was fully installed, built, type-checked (`tsc -b` — zero errors), and
  verified end-to-end against a temporary mock API standing in for the real backend
  (upload → mapping → validation → commit all confirmed working correctly, including
  duplicate detection, missing-mandatory-field errors, and warning-only rows still importing).
- The **backend** (`backend/BulkImportPlatform.*`) is complete, carefully written C# source
  code that mirrors the proposal's architecture, but it has **not** been compiled or run,
  because `dotnet restore` cannot reach NuGet from this sandbox. The one package-free project
  (`BulkImportPlatform.Core`, which only contains entities/enums/DTOs/interfaces) *was* built
  successfully here and compiles cleanly — that at least validates the shared domain model.
  The `Infrastructure` and `Api` projects (EF Core, SQL Server, ClosedXML, ASP.NET Core) could
  not be restored/built here, so please run a build on your own machine as the first step.

### First thing to do when you pick this up

```bash
cd backend
dotnet restore
dotnet build
```

Fix any errors that surface (there shouldn't be many — the code was written carefully against
known EF Core 8 / ClosedXML 0.104 / ASP.NET Core 8 APIs — but since it was never compiled here,
treat this as an unverified first build).

## Project Layout

```
backend/
  BulkImportPlatform.sln
  BulkImportPlatform.Core/            Domain entities, enums, DTOs, service interfaces
  BulkImportPlatform.Infrastructure/  EF Core DbContext, Excel/Mapping/Validation/Default-Value/Import engines
  BulkImportPlatform.Api/             ASP.NET Core Web API (controllers, Program.cs, appsettings)
  schema.sql                         Manual SQL Server schema (fallback if you don't want to run EF migrations)
frontend/
  src/
    api/client.ts                    Axios client for the backend API
    types/index.ts                   TypeScript mirrors of the backend DTOs
    pages/                           Dashboard, Vehicle/Customer wizard, History, Audit
    components/wizard/               Upload, Mapping, Preview, Result steps
```

## Running the backend

1. Have SQL Server reachable (LocalDB, a Docker container, or a real instance). Update the
   connection string in `backend/BulkImportPlatform.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`).
2. Create the schema, either via EF Core migrations (preferred, keeps schema in sync with the
   C# model going forward) or the provided raw SQL script:

   ```bash
   # Option A: EF Core migrations
   cd backend/BulkImportPlatform.Api
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate --project ../BulkImportPlatform.Infrastructure --startup-project .
   dotnet ef database update --project ../BulkImportPlatform.Infrastructure --startup-project .

   # Option B: run backend/schema.sql directly against an empty SQL Server database
   ```

3. Run the API:

   ```bash
   cd backend/BulkImportPlatform.Api
   dotnet run
   ```

   Swagger UI is available at `/swagger` once running (e.g. `https://localhost:5001/swagger`).
   Note the port dotnet prints and update the frontend's `.env` (`VITE_API_BASE_URL`) to match.

## Running the frontend

Already verified working in this session (build + type-check passed, full workflow tested
against a mock API).

```bash
cd frontend
npm install
npm run dev
```

Opens at `http://localhost:5173`. Set `VITE_API_BASE_URL` in `frontend/.env` to point at your
running backend (defaults to `http://localhost:5199` — change this to match whatever port your
`dotnet run` actually prints, e.g. `https://localhost:5001` or `http://localhost:5199`).

## What's implemented (per the Project Proposal)

**Vehicle Import Module** — mandatory field: Vehicle Number. Optional: VIN, Make, Model, Year,
Color, Fuel Type, Tank Size, Odometer, License Number, Spot Number, Transmission, Vehicle Type.
Critical errors: Vehicle Number missing, Vehicle Number already exists (checked against both
existing DB records and duplicates within the same file), invalid/corrupted file. Warnings: VIN,
Make, Model, Year, Fuel information missing.

**Customer Import Module** — mandatory field: Customer Name. Optional: Email, Phone, Address,
City, State, Country, Date of Birth, Driver License Number. Critical errors: Customer Name
missing, invalid/unprocessable file. Warnings: Email, Phone, Address missing.

**Platform Features** — Dashboard, upload management, validation engine, error/warning
reporting, import history, audit logging, Excel template management/download, configurable
column-to-field data mapping (with smart auto-suggestion and alias hints, e.g.
"Manufacturer" → Make), default value processing (e.g. Vehicle Year → current year, Tank Size →
0, Customer Status → Active) with clear indication in the review screen of which fields
received a default.

**Out of scope for this phase** (per the proposal, section 4): Contract/Invoice/Driver/Insurance
imports, scheduled imports, external system sync, workflow approvals.

## Known limitations / next steps

- No authentication yet (NFR: Security) — the API currently reads a plain `X-User` header for
  attribution. Wire up real auth before any production use.
- Large files (the proposal's NFR target is 1,000+ records per upload) should perform fine
  given the row-by-row processing design, but this hasn't been load-tested since the backend
  couldn't be run in this sandbox.
- No automated test project was included given the time constraints — worth adding unit tests
  for the Validation/Mapping/Default-Value engines before relying on this in production.
