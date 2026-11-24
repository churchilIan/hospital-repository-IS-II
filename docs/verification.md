# Verification record

## Static checks completed in this sandbox

- Recovered the source from the prior project archive.
- Removed the Docker Compose artifact and all Docker run instructions.
- Renamed the solution and namespaces to Hospital Repository.
- Added `MasterPatientId` to the domain patient model, API DTOs, EF mapping, patient search, and startup compatibility patch.
- Added deterministic synthetic seed records for two hospitals linked by `MPI-KE-10001`.
- Added browser pages for login, registration, and MPI search/registration.
- Added the PostgreSQL seed script at `database/01_hospital_repository_seed.sql`.
- Updated documentation for a local PostgreSQL installation.

## Target-machine checks

The active sandbox does not contain the .NET 8 SDK or PostgreSQL client, so compilation, EF migration execution, and integration tests could not be rerun here. On a workstation with the required tools, run:

```bash
dotnet restore HospitalRepositoryPlatform.sln
dotnet build HospitalRepositoryPlatform.sln
dotnet test HospitalRepositoryPlatform.sln
```

Then start PostgreSQL, run the web project, sign in with the development administrator, and verify:

```bash
curl http://localhost:5000/health
curl 'http://localhost:5000/api/patients?q=MPI-KE-10001' -H 'Authorization: Bearer YOUR_TOKEN'
```

The MPI search should return the Amina Otieno record for both Eastlands Community Hospital and Westlands Medical Centre.
