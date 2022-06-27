using System.Security.Cryptography.X509Certificates;

using Iot.Raspi.Logger;

namespace Iot.Raspi.Mqtt
{
    /// <summary>
    /// This class is used to validate the server certificate (SSL/TLS certificate) so that 
    /// the client can authenticate the server.
    /// </summary>
    public class Certificates
    {
        // Set logger context
        private static Serilog.ILogger Log => Serilog.Log.ForContext<Certificates>();

        private ConfigMqtt config;

        public Certificates(ConfigMqtt config)
        {
            this.config = config;
        }

        /// <summary>
        /// Validate a server certificate (SSL/TSL certificate) received from the server during connection setup.
        ///  - Check if the certificate was issued by a valid CA (certificate authority).
        ///  - Check the thumbprint/fingerprint of the server certificate.
        /// </summary>
        /// <param name="serverCertificate">Server certificate.</param>
        /// <returns>true if calidation is OK.</returns>
        public static bool ValidateServerCertificate(X509Certificate2 serverCertificate)
        {
            var caCertificate = LoadCaCertificate();

            var ok = ValidateCertificateChain(serverCertificate, caCertificate);
            ok = ok && ValidateCertificateThumbprint(serverCertificate, Config.Instance.Certificates.ServerCertificateThumbprint);
            return ok;
        }

        /// <summary>
        /// Check if the certificate was issued by a valid CA (certificate authority), 
        /// i.e. check the whole certificate chain.
        /// </summary>
        /// <param name="certificate">Server certificate.</param>
        /// <param name="caCertificate">CA certificate.</param>
        /// <returns>true if CA is OK</returns>
        private static bool ValidateCertificateChain(X509Certificate2 certificate, X509Certificate2? caCertificate = null)
        {
            X509Chain chain = new X509Chain();
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            if (caCertificate != null)
            {
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(caCertificate);
            }
            var ok = chain.Build(certificate);
            if (!ok)
            {
                // Error messages
                Log.Here().Error("Failed to verify certificate '{subject}'.", certificate.Subject);
                foreach (var status in chain.ChainStatus)
                {
                    Log.Here().Error(" - {reason}.", status.StatusInformation);
                }
            }
            return ok;
        }

        /// <summary>
        /// Check the thumbprint/fingerprint of the server certificate.
        /// </summary>
        /// <param name="certificate">Server certificate.</param>
        /// <param name="expectedThumbprint">Expected thumbpring/fingerprint.</param>
        /// <returns>true if thumbpring/fingerprint is OK</returns>
        private static bool ValidateCertificateThumbprint(X509Certificate2 certificate, string expectedThumbprint)
        {
            var thumbprint = certificate.GetCertHashString().ToLower();
            if (thumbprint != expectedThumbprint)
            {
                Console.WriteLine($"Verification of certificate thumbprint failed, certificate '{certificate.Subject}'.");
                return false;
            }    
            return true;
        }

        /// <summary>
        /// Load the CA certificate from file.
        /// </summary>
        /// <returns>CA certificate</returns>
        private static X509Certificate2? LoadCaCertificate()
        {
            var caCertFilePath = Path.Combine(Directory.GetCurrentDirectory(), Config.Instance.Certificates.CaCertificateFilePath);
            if (File.Exists(caCertFilePath))
            {
                var caCert = new X509Certificate2(File.ReadAllBytes(caCertFilePath));
                return caCert;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Load the client certificate from file.
        /// </summary>
        /// <returns>client certificate</returns>
        private static X509Certificate2? LoadClientCertificate()
        {
            var clientCertFilePath = Path.Combine(Directory.GetCurrentDirectory(), Config.Instance.Certificates.ClientCertificateFilePath);
            if (File.Exists(clientCertFilePath))
            {
                var clientCert = new X509Certificate2(File.ReadAllBytes(clientCertFilePath), Config.Instance.Certificates.ClientCertificatePassword);
                return clientCert;
            }
            else 
            {
                return null;
            }
        }
    }
}