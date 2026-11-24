using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HospitalRepository.Domain.Entities;

namespace HospitalRepository.Infrastructure;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    // The hospital this user belongs to.
    // Nullable because central system administrators
    // may not belong to a particular hospital.
    public Guid? InstitutionId { get; set; }

    public Institution? Institution { get; set; }
}

public sealed class PlatformDbContext : IdentityDbContext<ApplicationUser>
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientIdentifier> PatientIdentifiers => Set<PatientIdentifier>();
    public DbSet<ClinicalEncounter> ClinicalEncounters => Set<ClinicalEncounter>();
    public DbSet<LaboratoryResult> LaboratoryResults => Set<LaboratoryResult>();
    public DbSet<MedicationRecord> MedicationRecords => Set<MedicationRecord>();
    public DbSet<FhirResourceRecord> FhirResourceRecords => Set<FhirResourceRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Institution>(entity =>
        {
            entity.ToTable("institutions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.HasMany(x => x.Providers).WithOne(x => x.Institution).HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Patients).WithOne(x => x.ManagingInstitution).HasForeignKey(x => x.ManagingInstitutionId).OnDelete(DeleteBehavior.Restrict);
        });
        // Link each user account to its registered hospital.
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(x => x.Institution)
                  .WithMany()
                  .HasForeignKey(x => x.InstitutionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });


        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("providers");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.GivenName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.FamilyName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.RegistrationNumber)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => x.RegistrationNumber)
                .IsUnique();

            entity.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Link a Provider record to its Identity login account.
            entity.Property(x => x.UserId)
                .HasMaxLength(450);

            entity.HasIndex(x => x.UserId)
                .IsUnique();

            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<Provider>(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });


        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("patients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MedicalRecordNumber).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.MedicalRecordNumber).IsUnique();
            entity.Property(x => x.MasterPatientId).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.MasterPatientId);
            entity.Property(x => x.NationalIdentifier).HasMaxLength(100);
            entity.HasIndex(x => x.NationalIdentifier);
            entity.Property(x => x.GivenName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FamilyName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Sex).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.HasMany(x => x.Identifiers).WithOne(x => x.Patient).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatientIdentifier>(entity =>
        {
            entity.ToTable("patient_identifiers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.System).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(50);
            entity.HasIndex(x => new { x.System, x.Value }).IsUnique();
        });

        modelBuilder.Entity<ClinicalEncounter>(entity =>
        {
            entity.ToTable("clinical_encounters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Diagnosis).HasMaxLength(500);
            entity.Property(x => x.Procedure).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Institution).WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LaboratoryResult>(entity =>
        {
            entity.ToTable("laboratory_results");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Display).HasMaxLength(250).IsRequired();
            entity.Property(x => x.TextValue).HasMaxLength(500);
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.NumericValue).HasPrecision(18, 6);
            entity.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MedicationRecord>(entity =>
        {
            entity.ToTable("medication_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MedicationCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MedicationName).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Dosage).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Route).HasMaxLength(100);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FhirResourceRecord>(entity =>
        {
            entity.ToTable("fhir_resource_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ResourceId).HasMaxLength(100);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.HasIndex(x => new { x.ResourceType, x.ResourceId });
            entity.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorUserId).HasMaxLength(450);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.Outcome).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(500);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
