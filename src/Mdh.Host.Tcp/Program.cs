using Mdh.Core.Engine;
using Mdh.Core.Protocols;
using Mdh.Core.Vendors;
using Mdh.Host.Tcp;
using Mdh.Protocol.Poct1A;
using Mdh.Vendor.Demo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Configure options
        services.Configure<MdhOptions>(configuration.GetSection("Mdh"));

        // Register logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
        });

        // Register core services
        services.AddSingleton<VendorRegistry>();
        services.AddSingleton<IPoct1AParser, Poct1AParser>();
        services.AddSingleton<IAckBuilder, AckBuilder>();

        // Register demo vendor pack
        services.AddSingleton<IVendorDevicePack, DemoVendorPack>();
        services.AddSingleton<DemoVendorPack>();

        // Register handlers
        services.AddTransient<HelloHandler>();
        services.AddTransient<DeviceStatusHandler>();
        services.AddTransient<ObservationRequestHandler>();
        services.AddTransient<ObservationHandler>();
        services.AddTransient<ListUploadHandler>();

        // Register hosted service
        services.AddHostedService<TcpListenerHostedService>();
    })
    .Build();

// Register vendor packs
var vendorRegistry = host.Services.GetRequiredService<VendorRegistry>();
var demoPack = host.Services.GetRequiredService<DemoVendorPack>();
vendorRegistry.Register(demoPack);

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("MDH Multi-Device-Hub TCP Service starting...");

await host.RunAsync();
