using GoldsrcFramework;
using GoldsrcFramework.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoldsrcFramework.Demo
{
    /// <summary>
    /// Demo mod startup class
    /// This is the entry point for mod-specific initialization and service registration
    /// Similar to ASP.NET Core's Startup class or WPF's App class
    /// </summary>
    public class DemoModStartup : GoldsrcModStartup
    {
        /// <summary>
        /// Configure services for dependency injection
        /// This is called during framework initialization, before the service provider is built
        /// Use this to register your mod's custom services
        /// </summary>
        public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Call base implementation
            base.ConfigureServices(services, configuration);

            // Example: Register custom services
            // services.AddSingleton<IMyCustomService, MyCustomService>();
            // services.AddTransient<IMyTransientService, MyTransientService>();
            
            // Example: Configure custom options
            // services.Configure<MyModSettings>(configuration.GetSection("MyMod"));

            System.Diagnostics.Debug.WriteLine("[DemoModStartup] ConfigureServices called");
        }

        /// <summary>
        /// Configure the mod after services are built
        /// This is called after the service provider is built and ready to use
        /// Use this to perform initialization logic that requires services
        /// </summary>
        public override void Configure(IServiceProvider serviceProvider)
        {
            // Call base implementation (this sets up Logger)
            base.Configure(serviceProvider);

            // Now you can use services from the container
            Logger?.LogInformation("=== Demo Mod Startup ===");
            Logger?.LogInformation("Demo mod is initializing...");

            // Example: Get configuration and log settings
            var frameworkSettings = serviceProvider.GetService<IOptions<FrameworkSettings>>();
            if (frameworkSettings != null)
            {
                Logger?.LogInformation("Framework: {Name} v{Version}", 
                    frameworkSettings.Value.FrameworkName, 
                    frameworkSettings.Value.Version);
            }

            var gameSettings = serviceProvider.GetService<IOptions<GameSettings>>();
            if (gameSettings != null)
            {
                Logger?.LogInformation("Game: {GameName}, Max Players: {MaxPlayers}", 
                    gameSettings.Value.GameName, 
                    gameSettings.Value.MaxPlayers);

                // Example: Access custom settings
                if (gameSettings.Value.CustomSettings.TryGetValue("DemoMode", out var demoMode))
                {
                    Logger?.LogInformation("Demo Mode: {DemoMode}", demoMode);
                }
            }

            // Example: Initialize mod-specific systems
            // var myService = serviceProvider.GetService<IMyCustomService>();
            // myService?.Initialize();

            Logger?.LogInformation("Demo mod initialization complete!");
        }
    }
}

