using SmartBodyAI.Components;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;

namespace SmartBodyAI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            #region 加入設定強型別注入宣告
            builder.Services.Configure<SettingModel>(builder.Configuration
                .GetSection(MagicObjectHelper.SmartAppSettingKey));
            #endregion

            #region 客製化註冊服務
            builder.Services.AddDistributedMemoryCache(); // 加入這行
            builder.Services.AddScoped<SettingService>();
            builder.Services.AddScoped<SmartAppSettingService>();
            builder.Services.AddScoped<OAuthStateStoreService>();
            #endregion

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
