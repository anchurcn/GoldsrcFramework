using GoldsrcFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GoldsrcMod;

public sealed class ModStartup : GoldsrcModStartup
{
    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);
    }

    public override void Configure(IServiceProvider serviceProvider)
    {
        base.Configure(serviceProvider);
        Logger?.LogInformation("My GoldSrc Mod initialized.");
    }
}
