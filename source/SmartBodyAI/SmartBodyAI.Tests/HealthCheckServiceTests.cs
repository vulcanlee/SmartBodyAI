using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartBodyAI.Models;
using SmartBodyAI.Services;
using SmartBodyAI.Servicers;
using Xunit;

namespace SmartBodyAI.Tests;

public class HealthCheckServiceTests
{
    [Fact]
    public async Task GenerateAsync_IncludesSystemInformationFromBoundConfiguration()
    {
        var service = CreateService(new SettingModel
        {
            SystemInformation = new SystemInformationModel
            {
                SystemName = "身體組成 SMART App",
                SystemDescription = "身體組成 SMART App",
                SystemVersion = "1.2.35 (2026/04/05)"
            },
            FhirServerUrl = "https://fhir.example/fhir",
            RedirectUrl = "https://app.example/patient-information",
            ClientId = "smart-app",
            ClientSecret = "secret",
            InferenceHostApi = "https://inference.example/health",
            AuthorizationScope = "openid fhirUser profile",
            UploadDicomPath = Path.GetTempPath(),
            UploadDicomTempPath = Path.GetTempPath(),
            DicomImagePath = Path.GetTempPath(),
            AIResultPath = Path.GetTempPath()
        });

        var summary = await service.GenerateAsync(queryIss: null, queryLaunch: null, queryDebug: null);

        Assert.Equal("身體組成 SMART App", summary.SystemName);
        Assert.Equal("1.2.35 (2026/04/05)", summary.SystemVersion);
    }

    [Fact]
    public async Task GenerateAsync_UsesFallbacksWhenSystemInformationIsMissing()
    {
        var service = CreateService(new SettingModel
        {
            SystemInformation = new SystemInformationModel(),
            FhirServerUrl = "https://fhir.example/fhir",
            RedirectUrl = "https://app.example/patient-information",
            ClientId = "smart-app",
            ClientSecret = "secret",
            InferenceHostApi = "https://inference.example/health",
            AuthorizationScope = "openid fhirUser profile",
            UploadDicomPath = Path.GetTempPath(),
            UploadDicomTempPath = Path.GetTempPath(),
            DicomImagePath = Path.GetTempPath(),
            AIResultPath = Path.GetTempPath()
        });

        var summary = await service.GenerateAsync(queryIss: null, queryLaunch: null, queryDebug: null);

        Assert.Equal("SmartBodyAI Health Check", summary.SystemName);
        Assert.Equal(string.Empty, summary.SystemVersion);
    }

    private static HealthCheckService CreateService(SettingModel settingModel)
    {
        var settingService = new SettingService(Options.Create(settingModel));
        var smartAppSettingService = new SmartAppSettingService(settingService);
        var configuration = new ConfigurationBuilder().Build();
        var diagnosticsService = new ConfigurationDiagnosticsService(configuration, new TestWebHostEnvironment());

        return new HealthCheckService(
            settingService,
            smartAppSettingService,
            diagnosticsService,
            new StubSmartDiscoveryService(),
            new TestWebHostEnvironment(),
            configuration,
            NullLogger<HealthCheckService>.Instance);
    }

    private sealed class StubSmartDiscoveryService : ISmartDiscoveryService
    {
        public Task<SmartDiscoveryResult> DiscoverAsync(string fhirServerUrl, IEnumerable<string>? capabilitiesFromMetadata = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SmartDiscoveryResult
            {
                AuthorizeUrl = "https://auth.example/authorize",
                TokenUrl = "https://auth.example/token",
                MetadataSource = ".well-known/smart-configuration",
                Capabilities = ["launch-standalone", "context-standalone-patient", "permission-patient", "sso-openid-connect"]
            });
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SmartBodyAI.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
