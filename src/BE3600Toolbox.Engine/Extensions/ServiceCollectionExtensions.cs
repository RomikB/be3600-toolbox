using System.Net;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Engine.Flashing;
using BE3600Toolbox.Engine.MiWiFi;
using BE3600Toolbox.Engine.Settings;
using BE3600Toolbox.Engine.Ssh;
using Microsoft.Extensions.DependencyInjection;

namespace BE3600Toolbox.Engine.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBe3600Engine(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var cookieContainer = new CookieContainer();
            var handler = new SocketsHttpHandler
            {
                CookieContainer = cookieContainer,
                UseCookies = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            );

            return client;
        });

        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddTransient<IRouterService, MiWiFiClient>();
        services.AddSingleton<ISshManager, SshManager>();
        services.AddTransient<ISshConnectionService, SshConnectionService>();
        services.AddTransient<IFlashService, FlashService>();
        return services;
    }
}
