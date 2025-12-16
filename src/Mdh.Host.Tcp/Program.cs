using Mdh.Core.Engine;
using Mdh.Core.Protocols;
using Mdh.Core.Vendors;
using Mdh.Protocol.Poct1A.Builders;
using Mdh.Protocol.Poct1A.Parsing;
using Mdh.Vendor.Demo;
using Mdh.Vendor.Demo.Handlers;
using Mdh.Host.Tcp.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.Configure<MdhOptions>(context.Configuration.GetSection("Mdh"));

    services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    });

    // Vendor packs & registry
    services.AddSingleton<DemoAckRuleRegistry>();
    services.AddSingleton<DemoVendorPack>();
    services.AddSingleton<IVendorDevicePack>(sp => sp.GetRequiredService<DemoVendorPack>());
    services.AddSingleton<IPoct1AVendorPack>(sp => sp.GetRequiredService<DemoVendorPack>());
    services.AddSingleton<VendorRegistry>();

    // POCT1A utilities
    services.AddSingleton<IPoct1AParser, Poct1AParser>();
    services.AddSingleton<IPoct1AAckParser, Poct1AAckParser>();
    services.AddSingleton<IPoct1AAckBuilder, AckBuilder>();

    // Demo handler registrations (resolved by vendor pack pipeline)
    services.AddTransient<HelloHandler>();
    services.AddTransient<DeviceStatusHandler>();
    services.AddTransient<ObservationRequestHandler>();
    services.AddTransient<ObservationHandler>();
    services.AddTransient<ListUploadHandler>();

    // Engine
    services.AddTransient<SessionEngine>();

    // Hosted TCP listener
    services.AddHostedService<TcpListenerHostedService>();
});

await builder.Build().RunAsync();
