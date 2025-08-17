using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AgentHost.Api.Tests;

// Custom factory that hosts the app on real Kestrel instead of the in-memory TestServer
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseKestrel(options =>
        {
            // Allow synchronous IO (defensive; not strictly required)
            options.AllowSynchronousIO = true;
        });
        // Use dynamic port (0 chooses an available port)
        builder.UseUrls("http://127.0.0.1:0");

        builder.ConfigureServices(services => {
            // nothing yet
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build & start Kestrel host
        var host = builder.Build();
        host.Start();

        // Resolve the bound address and assign to client options
        var server = host.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        var baseAddress = addresses!.Addresses.First();
        ClientOptions.BaseAddress = new Uri(baseAddress);

        return host;
    }
}
