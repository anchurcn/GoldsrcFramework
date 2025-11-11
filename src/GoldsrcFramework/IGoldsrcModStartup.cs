using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoldsrcFramework
{
    /// <summary>
    /// Interface for mod startup configuration
    /// Similar to ASP.NET Core's Startup class pattern
    /// </summary>
    public interface IGoldsrcModStartup
    {
        /// <summary>
        /// Configure services for dependency injection
        /// This is called during framework initialization, before the service provider is built
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <param name="configuration">The configuration instance</param>
        void ConfigureServices(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// Configure the mod after services are built
        /// This is called after the service provider is built and ready to use
        /// </summary>
        /// <param name="serviceProvider">The built service provider</param>
        void Configure(IServiceProvider serviceProvider);
    }
}

