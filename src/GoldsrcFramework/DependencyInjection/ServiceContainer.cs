using GoldsrcFramework.Configuration;
using GoldsrcFramework.Engine.Native;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.Loader;

namespace GoldsrcFramework.DependencyInjection
{
    /// <summary>
    /// Service container for dependency injection
    /// </summary>
    public static class ServiceContainer
    {
        private static IServiceProvider? _serviceProvider;
        private static IConfiguration? _configuration;
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        private static IGoldsrcModStartup? _modStartup;

        /// <summary>
        /// Get the service provider
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    throw new InvalidOperationException("ServiceContainer has not been initialized. Call Initialize() first.");
                }
                return _serviceProvider;
            }
        }

        /// <summary>
        /// Get the configuration
        /// </summary>
        public static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    throw new InvalidOperationException("ServiceContainer has not been initialized. Call Initialize() first.");
                }
                return _configuration;
            }
        }

        /// <summary>
        /// Check if the container is initialized
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Initialize the service container
        /// </summary>
        public static void Initialize(string? configurationPath = null)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // Build configuration
                    var frameworkDir = Path.GetDirectoryName(typeof(ServiceContainer).Assembly.Location);
                    var configBuilder = new ConfigurationBuilder()
                        .SetBasePath(frameworkDir ?? Directory.GetCurrentDirectory());

                    // Only load modSettings.json
                    var modSettingsPath = Path.Combine(frameworkDir ?? "", "modSettings.json");
                    if (File.Exists(modSettingsPath))
                    {
                        configBuilder.AddJsonFile("modSettings.json", optional: false, reloadOnChange: false);
                    }
                    else
                    {
                        // Fallback: try modsettings.json (lowercase) for backward compatibility
                        var modSettingsPathLower = Path.Combine(frameworkDir ?? "", "modsettings.json");
                        if (File.Exists(modSettingsPathLower))
                        {
                            configBuilder.AddJsonFile("modsettings.json", optional: false, reloadOnChange: false);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Warning: modSettings.json not found!");
                        }
                    }

                    // Add custom configuration path if provided
                    if (!string.IsNullOrEmpty(configurationPath) && File.Exists(configurationPath))
                    {
                        configBuilder.AddJsonFile(configurationPath, optional: true, reloadOnChange: false);
                    }

                    _configuration = configBuilder.Build();

                    // Build service collection
                    var services = new ServiceCollection();

                    // Register configuration
                    services.AddSingleton(_configuration);

                    // Register options
                    services.Configure<FrameworkSettings>(_configuration.GetSection("Framework"));
                    services.Configure<LoggingSettings>(_configuration.GetSection("Logging"));
                    services.Configure<GameSettings>(_configuration.GetSection("Game"));

                    // Register logging
                    ConfigureLogging(services, _configuration);

                    // Register core services
                    ConfigureCoreServices(services, _configuration);

                    // Discover and invoke mod startup class
                    _modStartup = DiscoverModStartup(_configuration);
                    if (_modStartup != null)
                    {
                        // Initialize the startup instance with configuration
                        if (_modStartup is GoldsrcModStartup baseStartup)
                        {
                            baseStartup.Initialize(_configuration);
                        }

                        // Allow mod to configure services
                        _modStartup.ConfigureServices(services, _configuration);
                    }

                    // Build service provider
                    _serviceProvider = services.BuildServiceProvider();

                    // Call Configure on mod startup (after services are built)
                    if (_modStartup != null)
                    {
                        _modStartup.Configure(_serviceProvider);
                    }

                    _initialized = true;

                    // Log initialization
                    var logger = _serviceProvider.GetService<ILogger<object>>();
                    logger?.LogInformation("ServiceContainer initialized successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ServiceContainer initialization error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Configure logging services
        /// </summary>
        private static void ConfigureLogging(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(builder =>
            {
                // Get logging settings
                var loggingSection = configuration.GetSection("Logging");
                var minLevel = loggingSection.GetValue<string>("MinimumLevel") ?? "Information";
                var enableConsole = loggingSection.GetValue<bool>("EnableConsole", true);

                // Parse log level
                if (Enum.TryParse<LogLevel>(minLevel, out var logLevel))
                {
                    builder.SetMinimumLevel(logLevel);
                }

                // Add console logger if enabled
                if (enableConsole)
                {
                    builder.AddConsole();
                    builder.AddDebug();
                }

                // Configure log levels from configuration
                builder.AddConfiguration(loggingSection);
            });
        }

        /// <summary>
        /// Discover mod startup class from the configured game assembly
        /// </summary>
        private static IGoldsrcModStartup? DiscoverModStartup(IConfiguration configuration)
        {
            try
            {
                var frameworkSettings = configuration.GetSection("Framework");
                var clientAssemblyName = frameworkSettings.GetValue<string>("GameClientAssembly");
                var serverAssemblyName = frameworkSettings.GetValue<string>("GameServerAssembly");

                // Try client assembly first, then server assembly
                var assemblyName = clientAssemblyName ?? serverAssemblyName;

                if (string.IsNullOrEmpty(assemblyName))
                {
                    return null;
                }

                var frameworkDir = Path.GetDirectoryName(typeof(ServiceContainer).Assembly.Location);
                var assemblyPath = Path.Combine(frameworkDir!, assemblyName);

                if (!File.Exists(assemblyPath))
                {
                    return null;
                }

                // Load the assembly
                var assembly = AssemblyLoadContext.GetLoadContext(typeof(ServiceContainer).Assembly)!
                    .LoadFromAssemblyPath(assemblyPath);

                // Find a type that implements IGoldsrcModStartup
                var startupType = assembly.GetTypes()
                    .FirstOrDefault(t => !t.IsAbstract && !t.IsInterface &&
                                        typeof(IGoldsrcModStartup).IsAssignableFrom(t));

                if (startupType != null)
                {
                    var startup = (IGoldsrcModStartup)Activator.CreateInstance(startupType)!;
                    System.Diagnostics.Debug.WriteLine($"Discovered mod startup: {startupType.FullName}");
                    return startup;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to discover mod startup: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Configure core framework services
        /// </summary>
        private static void ConfigureCoreServices(IServiceCollection services, IConfiguration configuration)
        {
            // Register server exports
            services.AddSingleton<IServerExportFuncs>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<object>>();
                var frameworkSettings = configuration.GetSection("Framework");
                var serverAssemblyName = frameworkSettings.GetValue<string>("GameServerAssembly");

                if (!string.IsNullOrEmpty(serverAssemblyName))
                {
                    try
                    {
                        var frameworkDir = Path.GetDirectoryName(typeof(ServiceContainer).Assembly.Location);
                        var serverAssemblyPath = Path.Combine(frameworkDir!, serverAssemblyName);

                        if (File.Exists(serverAssemblyPath))
                        {
                            var assembly = AssemblyLoadContext.GetLoadContext(typeof(ServiceContainer).Assembly)!
                                .LoadFromAssemblyPath(serverAssemblyPath);

                            var serverType = assembly.GetTypes()
                                .FirstOrDefault(x => x.GetInterface(nameof(IServerExportFuncs)) == typeof(IServerExportFuncs));

                            if (serverType != null)
                            {
                                logger.LogInformation("Loading custom server implementation: {ServerType}", serverType.FullName);
                                return (IServerExportFuncs)Activator.CreateInstance(serverType)!;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to load custom server assembly: {AssemblyName}", serverAssemblyName);
                    }
                }

                logger.LogInformation("Using default server implementation: FrameworkServerExports");
                return new FrameworkServerExports();
            });

            // Register client exports
            services.AddSingleton<IClientExportFuncs>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<object>>();
                var frameworkSettings = configuration.GetSection("Framework");
                var clientAssemblyName = frameworkSettings.GetValue<string>("GameClientAssembly");

                if (!string.IsNullOrEmpty(clientAssemblyName))
                {
                    try
                    {
                        var frameworkDir = Path.GetDirectoryName(typeof(ServiceContainer).Assembly.Location);
                        var clientAssemblyPath = Path.Combine(frameworkDir!, clientAssemblyName);

                        if (File.Exists(clientAssemblyPath))
                        {
                            var assembly = AssemblyLoadContext.GetLoadContext(typeof(ServiceContainer).Assembly)!
                                .LoadFromAssemblyPath(clientAssemblyPath);

                            var clientType = assembly.GetTypes()
                                .FirstOrDefault(x => x.GetInterface(nameof(IClientExportFuncs)) == typeof(IClientExportFuncs));

                            if (clientType != null)
                            {
                                logger.LogInformation("Loading custom client implementation: {ClientType}", clientType.FullName);
                                return (IClientExportFuncs)Activator.CreateInstance(clientType)!;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to load custom client assembly: {AssemblyName}", clientAssemblyName);
                    }
                }

                logger.LogInformation("Using default client implementation: FrameworkClientExports");
                return new FrameworkClientExports();
            });
        }

        /// <summary>
        /// Get a service from the container
        /// </summary>
        public static T GetService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Get a service from the container (nullable)
        /// </summary>
        public static T? GetServiceOrNull<T>() where T : class
        {
            return ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// Reset the service container (for testing purposes)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                if (_serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _serviceProvider = null;
                _configuration = null;
                _initialized = false;
            }
        }
    }
}

