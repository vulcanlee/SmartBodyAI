
using AIAgent.Models;
using AIAgent.Services;
using CTMS.Business.Services.ClinicalInformation;
using CTMS.DataModel.Models;
using CTMS.Share.Helpers;
using SyncExcel.Services;

namespace SmartAgentApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            #region 客製化服務註冊
            builder.Services.AddScoped<AgentService>();
            builder.Services.AddScoped<PatientAIInfoService>();
            builder.Services.AddScoped<Phase1Phase2Service>();
            builder.Services.AddScoped<DirectoryHelperService>();
            builder.Services.AddScoped<AgentService>();
            builder.Services.AddScoped<AIIntegrateService>();

            builder.Services.AddScoped<CurrentProject>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<RequestInformation>();

            builder.Services.AddTransient<RandomListService>();
            builder.Services.AddTransient<AgentService>();
            builder.Services.AddTransient<PatientAIInfoService>();
            builder.Services.AddTransient<Phase1Phase2Service>();
            builder.Services.AddTransient<DirectoryHelperService>();
            builder.Services.AddTransient<RiskAssessmentExcelService>();
            builder.Services.AddTransient<InputCsvService>();
            #endregion

            #region 加入設定強型別注入宣告
            builder.Services.Configure<Agentsetting>(builder.Configuration
                .GetSection(MagicObjectHelper.Agentsetting));
            #endregion

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
