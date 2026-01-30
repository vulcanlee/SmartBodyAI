
using AIAgent.Models;
using AIAgent.Services;
using CTMS.Share.Helpers;

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
