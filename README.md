# Hospital Repository

Hospital Repository is an ASP.NET Core 8 and PostgreSQL final-year BBIT project for secure healthcare data exchange using HL7 FHIR R4. It links patient records from different hospitals through a shared Master Patient Index (MPI) while preserving each hospital's local medical record number.

This repository contains the complete Clean Architecture source, PostgreSQL seed SQL, REST APIs, Razor Pages, ASP.NET Identity/JWT authentication, EF Core migrations, FHIR validation/exchange integration, and tests.

> Use synthetic data only. This is an academic demonstration and is not approved for real patient data without consent management, encryption, key management, retention, monitoring, penetration testing, and applicable health privacy controls.

## Project structure

| Project | Responsibility |
|---|---|
| `HospitalRepository.Domain` | Entities, enums, invariants, and the shared `MasterPatientId`. |
| `HospitalRepository.Application` | DTOs, service contracts, and repository-facing use-case abstractions. |
| `HospitalRepository.Infrastructure` | EF Core/PostgreSQL, Identity, JWT, FHIR SDK, Firely HTTP exchange, services, migrations, and seed logic. |
| `HospitalRepository.Web` | Razor Pages, Bootstrap UI, REST API controllers, Swagger, authentication, and request auditing. |
| `HospitalRepository.Tests` | Domain and service tests. |

## Requirements without Docker

Install these on the machine where you will run the application:

1. .NET 8 SDK
2. PostgreSQL 14 or later
3. Optional: `dotnet-ef` for creating and applying migrations manually

Create the PostgreSQL database and user using `psql` or pgAdmin:

```sql
CREATE USER hospital_app WITH PASSWORD 'change-this-password';
CREATE DATABASE hospital_repository OWNER hospital_app;
```

Update `src/HospitalRepository.Web/appsettings.json` or, preferably, set the environment variable:

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=hospital_repository;Username=hospital_app;Password=change-this-password'
```

There is no Dockerfile or `docker-compose.yml` in this project.

## Run

From the repository root:

```bash
dotnet restore HospitalRepositoryPlatform.sln
dotnet build HospitalRepositoryPlatform.sln
dotnet run --project src/HospitalRepository.Web
```

With `Database:ApplyMigrations` and `Database:SeedOnStartup` enabled, startup applies EF Core migrations, creates the roles and demo administrator, adds the MPI compatibility column, and seeds two hospitals plus two linked records for the same synthetic patient.

Demo administrator:

| Field | Value |
|---|---|
| Email | `admin@hospitalrepository.local` |
| Password | `Admin1234` |
| Role | `Administrator` |

Change the password and JWT key before sharing the application.

Open the URLs printed by ASP.NET Core:

- `/` — Hospital Repository dashboard
- `/Account/Login` — JWT login page
- `/Account/Register` — account registration for Provider, Patient, or ClinicalOfficer
- `/Patients` — browser-based MPI search and patient registration
- `/swagger` — REST API documentation in Development
- `/health` — health check

## How the two-hospital MPI link works

`patients.MasterPatientId` is the cross-hospital identity. `patients.MedicalRecordNumber` remains local to each hospital.

The seed creates:

| Hospital | Local MRN | Shared MPI |
|---|---|---|
| Eastlands Community Hospital (`EHC-01`) | `EHC-MRN-1001` | `MPI-KE-10001` |
| Westlands Medical Centre (`WMC-01`) | `WMC-MRN-7788` | `MPI-KE-10001` |

Searching `/api/patients?q=MPI-KE-10001` returns both records. The Patients Razor Page performs this request with JavaScript and renders the current PostgreSQL response, so database changes are reflected when the page is refreshed.

## Database SQL

After applying migrations, run the idempotent demonstration seed:

```bash
psql "$ConnectionStrings__DefaultConnection" -f database/01_hospital_repository_seed.sql
```

The script also shows a verification query at the end. It is safe to run repeatedly because it uses conflict handling and synthetic fixed IDs.

## API examples

Login:

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@hospitalrepository.local","password":"Admin1234"}'
```

Search the linked MPI records:

```bash
curl 'http://localhost:5000/api/patients?q=MPI-KE-10001' \
  -H 'Authorization: Bearer YOUR_TOKEN'
```

Register another hospital-specific patient record linked to the same person:

```bash
curl -X POST http://localhost:5000/api/patients \
  -H 'Authorization: Bearer YOUR_TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{"medicalRecordNumber":"NEW-MRN-9000","masterPatientId":"MPI-KE-10001","givenName":"Amina","familyName":"Otieno","dateOfBirth":"1992-04-18","sex":"female","managingInstitutionId":"INSTITUTION_GUID","nationalIdentifier":"NAT-KE-10001","phone":"+254700000001"}'
```

Core routes include `/api/auth`, `/api/institutions`, `/api/providers`, `/api/patients`, `/api/encounters`, `/api/laboratory-results`, `/api/medications`, `/api/fhir`, and `/api/audit`.

## FHIR R4

The FHIR module validates `Patient`, `Observation`, `Encounter`, `MedicationRequest`, `MedicationStatement`, `Practitioner`, and `Organization` payloads using the Firely .NET SDK package `Hl7.Fhir.R4`. If `FirelyServer:BaseUrl` is configured, validated resources can be sent to the external Firely Server endpoint. If it is blank, the application records a safe failed exchange rather than claiming synchronization succeeded.

## Migrations and tests

Create a later migration after model changes:

```bash
dotnet ef migrations add DescribeChange \
  --project src/HospitalRepository.Infrastructure/HospitalRepository.Infrastructure.csproj \
  --startup-project src/HospitalRepository.Web/HospitalRepository.Web.csproj \
  --output-dir Migrations
```

Run the solution tests:

```bash
dotnet test HospitalRepositoryPlatform.sln
```

The current sandbox did not have the .NET SDK installed, so build/test execution must be performed on the target .NET 8/PostgreSQL workstation.
