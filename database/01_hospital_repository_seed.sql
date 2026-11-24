-- Hospital Repository PostgreSQL seed
-- Run after: dotnet ef database update
-- This script is idempotent and uses synthetic data only.

ALTER TABLE IF EXISTS patients ADD COLUMN IF NOT EXISTS "MasterPatientId" varchar(100);
UPDATE patients SET "MasterPatientId" = 'MPI-' || replace("Id"::text, '-', '') WHERE "MasterPatientId" IS NULL;
ALTER TABLE IF EXISTS patients ALTER COLUMN "MasterPatientId" SET NOT NULL;
CREATE INDEX IF NOT EXISTS ix_patients_master_patient_id ON patients ("MasterPatientId");

INSERT INTO institutions ("Id", "CreatedAtUtc", "Name", "Code", "Address", "IsActive")
VALUES
 ('11111111-1111-1111-1111-111111111111', now(), 'Eastlands Community Hospital', 'EHC-01', '12 Umoja Avenue', true),
 ('22222222-2222-2222-2222-222222222222', now(), 'Westlands Medical Centre', 'WMC-01', '45 Ring Road', true)
ON CONFLICT ("Code") DO UPDATE SET "Name" = EXCLUDED."Name", "Address" = EXCLUDED."Address", "IsActive" = true;

INSERT INTO patients ("Id", "CreatedAtUtc", "MedicalRecordNumber", "MasterPatientId", "NationalIdentifier", "GivenName", "FamilyName", "DateOfBirth", "Sex", "Phone", "ManagingInstitutionId")
VALUES
 ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'EHC-MRN-1001', 'MPI-KE-10001', 'NAT-KE-10001', 'Amina', 'Otieno', DATE '1992-04-18', 'female', '+254700000001', (SELECT "Id" FROM institutions WHERE "Code" = 'EHC-01')),
 ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', now(), 'WMC-MRN-7788', 'MPI-KE-10001', 'NAT-KE-10001', 'Amina', 'Otieno', DATE '1992-04-18', 'female', '+254700000001', (SELECT "Id" FROM institutions WHERE "Code" = 'WMC-01'))
ON CONFLICT ("MedicalRecordNumber") DO UPDATE SET "MasterPatientId" = EXCLUDED."MasterPatientId", "ManagingInstitutionId" = EXCLUDED."ManagingInstitutionId";

-- Verify that one MPI identifier links two local hospital records:
SELECT p."MasterPatientId", p."MedicalRecordNumber", i."Name" AS hospital
FROM patients p JOIN institutions i ON i."Id" = p."ManagingInstitutionId"
WHERE p."MasterPatientId" = 'MPI-KE-10001'
ORDER BY i."Code";
