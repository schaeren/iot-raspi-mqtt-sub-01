using Microsoft.Extensions.Configuration;

using Iot.Raspi.Logger;

namespace Iot.Raspi.Mqtt
{
    // This class implements access to config date read from config file.
    // See classes ConfigMqtt, ConfigCertificates and ConfigInputs for more information.
    public class Config
    {
        // Set logger context
        private static Serilog.ILogger Log => Serilog.Log.ForContext<Config>();

        // Path of config filename relative to working directory
        public string ConfigFilePath { get; } = "appsettings.json";

        // The config sections
        public ConfigMqtt Mqtt { get; private set; } = default!;
        public ConfigCertificates Certificates { get; private set; } = default!;
        public ConfigOutputs Outputs { get; private set; } = default!;

        private static Config? instance; 

        // Sigleton: get access to Config object.
        public static Config Instance
        {
            get 
            {
                if (instance == null)
                {
                    instance = new Config();
                    instance.Load();
                }
                return instance;
            }
        }

        private Config()
        { }

        public void Load()
        {
            try
            {
                Log.Here().Verbose("Reading configuration from {configFile} ...", ConfigFilePath);

                var currentDir = Directory.GetCurrentDirectory();
                IConfiguration config = new ConfigurationBuilder()
                                    .SetBasePath(currentDir)
                                    .AddJsonFile(ConfigFilePath)
                                    .AddEnvironmentVariables()
                                    .Build();
                Mqtt = config.GetRequiredSection("mqtt").Get<ConfigMqtt>();
                Certificates = config.GetSection("certificates").Get<ConfigCertificates>();
                Outputs = config.GetRequiredSection("outputs").Get<ConfigOutputs>();            

                Log.Here().Information("Configuration initialized from {configFile}.", ConfigFilePath);
            }
            catch (Exception ex)
            {
                Log.Here().Fatal("Error while reading {configFile}: {exception}", ConfigFilePath, ex.Message);
                throw new Exception($"Failed to read configuration.");
            }
        }
    }
}