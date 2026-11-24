# Hospital Repository architecture

## Purpose

Hospital Repository is a Clean Architecture ASP.NET Core 8 application that gives EHR, LIS, pharmacy, and HMIS workflows a shared interoperability layer. PostgreSQL is the system of record for the application database; Firely Server is an optional external FHIR endpoint.

## Dependency direction

- **Domain** contains entities and invariants and depends on nothing.
- **Application** contains records, service contracts, and use-case abstractions and depends only on Domain.
- **Infrastructure** implements PostgreSQL persistence, Identity, JWT, FHIR validation, exchange, and application services.
- **Web** exposes APIs and Razor Pages, configures dependency injection, and captures request audit events.

Repositories/services are injected through interfaces. EF Core operations are asynchronous. Controllers do not directly manipulate EF entities; they call application service interfaces.

## MPI relationship

A `Patient` has both:

- `MedicalRecordNumber`: the local identifier assigned by a managing hospital. It is unique in the repository and can differ between hospitals.
- `MasterPatientId`: the stable cross-hospital identity used to find records belonging to the same person.

Example:

```text
Eastlands Community Hospital  EHC-MRN-1001  MPI-KE-10001
Westlands Medical Centre       WMC-MRN-7788  MPI-KE-10001
```

The API searches names, local MRNs, national identifiers, patient identifiers, and `MasterPatientId`. This preserves source-system context while enabling an MPI view across institutions.

## Modules

1. Authentication: registration, login, ASP.NET Identity, roles, JWT.
2. Institutions: organization registry and hospital ownership.
3. Providers: doctor, nurse, pharmacist, laboratory scientist, and administrator records.
4. Patients/MPI: patient registration, local identifiers, shared MPI search.
5. Clinical: encounters, diagnoses, procedures.
6. Laboratory: Observation-style results.
7. Pharmacy: medication records.
8. FHIR: R4 validation, local resource traceability, optional Firely exchange.
9. Audit: access and API action logging.
10. Web UI: dashboard, login, registration, MPI search, and patient registration.

## Browser-to-database flow

1. A user opens `/Account/Login` or `/Account/Register`.
2. JavaScript posts JSON to `/api/auth/login` or `/api/auth/register`.
3. The API uses ASP.NET Identity against PostgreSQL and returns a JWT for login.
4. `/Patients` stores the JWT in browser local storage for this academic demo.
5. The page sends `Authorization: Bearer` requests to `/api/institutions` and `/api/patients`.
6. The application service validates the request, persists through EF Core/Npgsql, and returns DTOs.
7. The page renders the returned data and refreshes after a successful registration.

Production deployments should use an HttpOnly secure cookie or a hardened token strategy rather than local storage, plus HTTPS and CSRF protections appropriate to the chosen authentication model.

## FHIR R4 mapping

| Hospital Repository capability | FHIR resource |
|---|---|
| MPI patient | Patient |
| Laboratory result | Observation |
| Clinical visit | Encounter |
| Medication | MedicationRequest / MedicationStatement |
| Provider | Practitioner |
| Hospital | Organization |
| Validation failure | OperationOutcome |

FHIR exchange is intentionally separated from local operational persistence. Every exchange attempt is indexed locally with status, payload, error, and synchronization timestamp.
