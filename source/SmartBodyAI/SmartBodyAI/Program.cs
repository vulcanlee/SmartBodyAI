using Microsoft.Extensions.FileProviders;
using NLog;
using NLog.Web;
using SmartBodyAI.Components;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;
using SmartBodyAI.Services;
using Syncfusion.Blazor;

namespace SmartBodyAI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logger = LogManager.Setup()
                .LoadConfigurationFromFile("nlog.config")
                .GetCurrentClassLogger();
            try
            {
                logger.Info("Starting SmartBodyAI application");
                var builder = WebApplication.CreateBuilder(args);

                builder.Logging.ClearProviders();
                builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                builder.Host.UseNLog();

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();


                builder.Services.AddAntDesign();
                builder.Services.AddSyncfusionBlazor();

                #region 加入設定強型別注入宣告
                builder.Services.Configure<SettingModel>(builder.Configuration
                    .GetSection(MagicObjectHelper.SmartAppSettingKey));
                #endregion

                #region 客製化註冊服務
                builder.Services.AddDistributedMemoryCache();
                builder.Services.AddHttpClient();
                builder.Services.AddScoped<SettingService>();
                builder.Services.AddScoped<SmartAppSettingService>();
                builder.Services.AddScoped<OAuthStateStoreService>();
                builder.Services.AddScoped<DicomService>();
                builder.Services.AddScoped<ConfigurationDiagnosticsService>();
                builder.Services.AddScoped<HealthCheckService>();
                builder.Services.AddScoped<SmartChecklistTemplateService>();
                builder.Services.AddScoped<SmartChecklistPersistenceService>();
                builder.Services.AddScoped<ISmartDiscoveryService, SmartDiscoveryService>();
                builder.Services.AddScoped<ISmartAuthorizationService, SmartAuthorizationService>();
                builder.Services.AddAntDesign();
                #endregion

                var app = builder.Build();

                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("NDE5MzI3MEAzMjM2MmUzMDJlMzBkZHZWVnBlRUJiTUZ4TzJwcUZ5T1hjT2g0alNtU1JlSVYwcG1XbkZ1ZWhjPQ==");

                // Configure the HTTP request pipeline.
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Error");
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
                app.UseHttpsRedirection();

                #region 靜態檔案路徑綁定
                var settingModel = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SettingModel>>().Value;
                MagicObjectHelper.UploadDicomTempPath = settingModel.UploadDicomTempPath;
                MagicObjectHelper.DicomImagePath = settingModel.DicomImagePath;
                MagicObjectHelper.UploadDicomPath = settingModel.UploadDicomPath;
                if (Directory.Exists(MagicObjectHelper.UploadDicomPath) == false)
                {
                    Directory.CreateDirectory(MagicObjectHelper.UploadDicomPath);
                }
                if (Directory.Exists(MagicObjectHelper.DicomImagePath) == false)
                {
                    Directory.CreateDirectory(MagicObjectHelper.DicomImagePath);
                }
                if (Directory.Exists(MagicObjectHelper.UploadDicomTempPath) == false)
                {
                    Directory.CreateDirectory(MagicObjectHelper.UploadDicomTempPath);
                }

                string filename = "sample1.png";
                string prepareImageSourcePath = Path.Combine("Datas", filename);
                string prepareImageTargetPath = Path.Combine(MagicObjectHelper.DicomImagePath, filename);
                File.Copy(prepareImageSourcePath, prepareImageTargetPath, true);
                filename = "sample2.png";
                prepareImageSourcePath = Path.Combine("Datas", filename);
                prepareImageTargetPath = Path.Combine(MagicObjectHelper.DicomImagePath, filename);
                File.Copy(prepareImageSourcePath, prepareImageTargetPath, true);
                app.UseStaticFiles();
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(MagicObjectHelper.DicomImagePath),
                    RequestPath = MagicObjectHelper.DicomWebPath
                });

                #endregion
                app.UseAntiforgery();

                app.MapStaticAssets();
                app.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();

                app.Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Stopped program because of an exception");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }
    }
}
