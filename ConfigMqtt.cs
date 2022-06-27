namespace Iot.Raspi.Mqtt
{
    public class ConfigMqtt
    {
        /// <summary>Identification of the client. See also https://www.hivemq.com/blog/mqtt-essentials-part-3-client-broker-connection-establishment/</summary>
        public string ClientId { get; set; } = "";
        /// <summary>
        /// false -> use unencrypted connection should be used for tests only.
        /// true -> use secure connection (TLS 1.3) and optionnaly authentication with client certificate.
        /// </summary>
        public bool UseSecureConnection { get; set; } = true;
        /// <summary>Use client certificate for authentication. Valid only if UseSecureConnection = true.</summary>
        public bool UseClientCertificate { get; set; } = true;
        /// <summary>Fully qualified hostname or IP address of MQTT broker.</summary>
        public string BrokerHost { get; set; } = "localhost";
        /// <summary>Unsecure port number to connect on MQTT broker, used if UseSecureConnection = false.</summary>
        public int BrockerPort { get; set; } = 1883;
        /// <summary>Secure port number to connect on MQTT broker, used if UseSecureConnection = true.</summary>
        public int BrockerSecurePort { get; set; } = 8883;
        /// <summary>
        /// Username used for authentication. It depends on type and configuration of the MQTT broker whether the 
        /// username is used together with the client certificate or not.
        /// </summary>
        public string Username { get; set; } = "";
        /// <summary>Password. Usually used only without client certificate.</summary>
        public string Password { get; set; } = "";
        /// <summary>Time in seconds between failed connevtion requests. This delay is also used if a connection has been lost.</summary>
        public int AutoReconnectDelay { get; set; } = 5;
        /// <summary>Time in seconds between PING requests sent to the MQTT broker.</summary>
        public int KeepAlivePeriod { get; set; } = 15;
    }
}