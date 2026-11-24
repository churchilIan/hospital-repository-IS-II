
namespace HospitalRepository.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http;
using HospitalRepository.Application;
using HospitalRepository.Domain.Entities;
using HospitalRepository.Infrastructure;

public sealed class DomainTests
{
    [Fact]
    public void Patient_adds_identifier_and_normalizes_contact_data()
    {
        var institutionId = Guid.NewGuid();
        var patient = new Patient("mrn-001", "Ada", "Lovelace", new DateOnly(1815, 12, 10), "female", institutionId);
        patient.SetPhone("  +44 20 0000 0000 ");
        patient.AddIdentifier("urn:test", "abc-123", "MR");

        Assert.Equal("+44 20 0000 0000", patient.Phone);
        var identifier = Assert.Single(patient.Identifiers);
        Assert.Equal("abc-123", identifier.Value);
        Assert.Equal("MR", identifier.Type);
    }

    [Fact]
    public void Institution_normalizes_code_and_rejects_missing_name()
    {
        var institution = new Institution("Central Hospital", " ch-01 ");
        Assert.Equal("CH-01", institution.Code);
        Assert.Throws<ArgumentException>(() => new Institution("", "CH-02"));
    }
}

public sealed class ServiceTests
{
    [Fact]
    public async Task Patient_service_creates_and_searches_by_identifier()
    {
        await using var db = CreateDb();
        var audit = new AuditService(db);
        var institutionService = new InstitutionService(db, audit);

        var institution = await institutionService.CreateAsync(
            new CreateInstitutionRequest(
                "Central Hospital",
                "CH-01",
                null,
                null,
                null));

        var patientService = new PatientService(db, audit);

        var created = await patientService.CreateAsync(new CreatePatientRequest("MRN-100", "Grace", "Hopper", new DateOnly(1906, 12, 9), "female", institution.Id, null, null, "urn:mpi", "MPI-100"));
        var matches = await patientService.SearchAsync("MPI-100");

        Assert.Equal(created.Id, Assert.Single(matches).Id);
        Assert.Equal("Central Hospital", created.ManagingInstitutionName);
    }

    [Fact]
    public async Task Fhir_service_accepts_supported_r4_patient_and_rejects_unknown_resource()
    {
        await using var db = CreateDb();
        var service = new FhirService(db, new FakeHttpClientFactory(), Options.Create(new FirelyServerOptions()), new AuditService(db));

        var valid = await service.ValidateAsync("{\"resourceType\":\"Patient\",\"id\":\"p-1\",\"name\":[{\"family\":\"Hopper\"}]}");
        var invalid = await service.ValidateAsync("{\"resourceType\":\"Bundle\"}");

        Assert.True(valid.IsValid);
        Assert.Equal("Patient", valid.ResourceType);
        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Issues, issue => issue.Contains("not enabled"));
    }

    private static PlatformDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlatformDbContext(options);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
