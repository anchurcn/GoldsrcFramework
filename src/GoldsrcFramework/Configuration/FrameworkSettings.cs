namespace GoldsrcFramework.Configuration
{
    /// <summary>
    /// Framework configuration settings
    /// </summary>
    public class FrameworkSettings
    {
        /// <summary>
        /// Framework name
        /// </summary>
        public string FrameworkName { get; set; } = "GoldsrcFramework";

        /// <summary>
        /// Framework version
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Enable debug mode
        /// </summary>
        public bool EnableDebug { get; set; } = false;

        /// <summary>
        /// Enable verbose logging
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// Game server assembly name
        /// </summary>
        public string? GameServerAssembly { get; set; }

        /// <summary>
        /// Game client assembly name
        /// </summary>
        public string? GameClientAssembly { get; set; }
    }

    /// <summary>
    /// Logging configuration settings
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Minimum log level (Trace, Debug, Information, Warning, Error, Critical)
        /// </summary>
        public string MinimumLevel { get; set; } = "Information";

        /// <summary>
        /// Enable console logging
        /// </summary>
        public bool EnableConsole { get; set; } = true;

        /// <summary>
        /// Enable file logging
        /// </summary>
        public bool EnableFile { get; set; } = false;

        /// <summary>
        /// Log file path
        /// </summary>
        public string? LogFilePath { get; set; }
    }

    /// <summary>
    /// Game configuration settings
    /// </summary>
    public class GameSettings
    {
        /// <summary>
        /// Game name
        /// </summary>
        public string GameName { get; set; } = "Half-Life";

        /// <summary>
        /// Max players
        /// </summary>
        public int MaxPlayers { get; set; } = 32;

        /// <summary>
        /// Enable physics
        /// </summary>
        public bool EnablePhysics { get; set; } = true;

        /// <summary>
        /// Physics update rate (Hz)
        /// </summary>
        public int PhysicsUpdateRate { get; set; } = 60;

        /// <summary>
        /// Custom game settings
        /// </summary>
        public Dictionary<string, string> CustomSettings { get; set; } = new();
    }
}

