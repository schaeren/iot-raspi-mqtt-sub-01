namespace Iot.Raspi.Mqtt
{
    public class ConfigCertificates
    {
        /// <summary>
        /// Thumbprint/fingerprint of the SSL/TLS certificate used by the MQTT server. 
        /// Used by the client to authenticate the server.
        /// </summary>
        public string ServerCertificateThumbprint { get; set; } = "";
        /// <summary>
        /// File path of the 'Certificate Authority' (CA) certificate. 
        /// Absolute path or path relative to the working directory.
        /// </summary>
        public string CaCertificateFilePath { get; set; } = "ca.crt";
        /// <summary>
        /// File path of the client certificate.
        /// Absolute path or path relative to the working directory.
        /// </summary>
        public string ClientCertificateFilePath { get; set; } = "client.pfx";
        /// <summary>Password to be used for reading the client certificate.</summary>
        public string ClientCertificatePassword { get; set; } = "password";
    }
}