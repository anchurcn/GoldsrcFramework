using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GoldsrcFramework
{
    /// <summary>
    /// Base class for mod startup configuration
    /// Inherit from this class in your mod project to configure services and initialization
    /// Similar to ASP.NET Core's Startup class or WPF's App class
    /// </summary>
    public abstract class GoldsrcModStartup : IGoldsrcModStartup
    {
        /// <summary>
        /// The configuration instance
        /// </summary>
        protected IConfiguration Configuration { get; private set; } = null!;

        /// <summary>
        /// The logger instance (available after ConfigureServices is called)
        /// </summary>
        protected ILogger? Logger { get; private set; }

        /// <summary>
        /// Internal initialization - sets up the configuration
        /// </summary>
        internal void Initialize(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Configure services for dependency injection
        /// Override this method to register your mod's services
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <param name="configuration">The configuration instance</param>
        public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Default implementation - can be overridden by derived classes
        }

        /// <summary>
        /// Configure the mod after services are built
        /// Override this method to perform initialization logic that requires services
        /// </summary>
        /// <param name="serviceProvider">The built service provider</param>
        public virtual void Configure(IServiceProvider serviceProvider)
        {
            // Get logger for derived classes to use
            Logger = serviceProvider.GetService<ILogger<GoldsrcModStartup>>();
            
            // Default implementation - can be overridden by derived classes
        }
    }
}

