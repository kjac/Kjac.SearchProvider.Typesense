// See https://aka.ms/new-console-template for more information

using Kjac.SearchProvider.Typesense.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Sync;

var applicationBuilder = Host.CreateApplicationBuilder();

applicationBuilder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);

applicationBuilder.Configuration.AddJsonFile("appsettings.json");

applicationBuilder.Services
    .AddTypesense(applicationBuilder.Configuration)
    .AddSingleton<IServerRoleAccessor, SingleServerRoleAccessor>()
    .AddTransient<Benchmarker>();

var host = applicationBuilder.Build();
await host.Services.GetRequiredService<Benchmarker>().RunAsync();
