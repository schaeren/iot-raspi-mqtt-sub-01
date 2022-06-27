using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Formatter;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using Serilog;

using Iot.Raspi.Logger;

namespace Iot.Raspi.Mqtt
{
    /// <summary>
    /// Thin wrapper around MQTTnet.Extensions.ManagedClient.IManagedMqttClient, see also 
    /// https://github.com/dotnet/MQTTnet/wiki/ManagedClient
    /// The main goal of this class is to remove most of the MQTTnet specific stuff from the 
    /// application:
    ///  - Initialization with setup of connection to MQTT broker with TLS 1.3 and client certificate.
    ///  - Subscriptions for topics: Allow to register individual callback functions for individual 
    ///    topics (including # and + wildcards).
    ///  - Publish topics.
    /// </summary>
    public class MqttClient
    {
        // Set logger context
        private static Serilog.ILogger Log => Serilog.Log.ForContext<MqttClient>();

        public delegate void TopicChangedEventHandler(MqttApplicationMessageReceivedEventArgs ev);

        private MqttProtocolVersion protocolVersion;
        private IManagedMqttClient managedMqttClient;
        private Dictionary<string, TopicChangedEventHandler> topicChangedEventHandlers;
        
        /// <summary>
        /// Ctor. Create IManagedMqttClient and add some handler (callback) functions.
        /// </summary>
        /// <param name="protocolVersion">Protocol version to use.</param>
        public MqttClient(MqttProtocolVersion protocolVersion = MqttProtocolVersion.V311)
        {
            Log.Here().Verbose("Ctor ...");
            this.protocolVersion = protocolVersion;
            topicChangedEventHandlers = new Dictionary<string, TopicChangedEventHandler>();
            
            // Create IMagagedMqttClient, see also https://github.com/dotnet/MQTTnet/wiki/Client
            managedMqttClient = new MqttFactory().CreateManagedMqttClient();
            managedMqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(this.OnConnectingFailed);
            managedMqttClient.ApplicationMessageProcessedHandler = new ApplicationMessageProcessedHandlerDelegate(this.OnMessagePublished);
            managedMqttClient.UseApplicationMessageReceivedHandler(ev => OnMessageReceived(ev));
        }

        /// <summary>
        /// Start IManagedMqttClient.  
        /// </summary>
        /// <remarks>
        /// Subscriptions should be added before calling this method to ensure not to lose any
        /// retained messages (retain flag set to true) published already before this method is 
        /// called.
        /// </remarks>
        /// <returns>void</returns>
        public async Task StartAsync()
        {
            Log.Here().Verbose("Starting MQTT client ...");
            await managedMqttClient.StartAsync(GetManagedClientOptions());
            Log.Here().Information("MQTT client started.");
        }

        //TODO ... PublishMessage(..., byte[] payload, ...) or IEnumerable<byte> payload

        /// <summary>
        /// Publish a message to MQTT broker.
        /// </summary>
        /// <param name="topic">Topic of the message.</param>
        /// <param name="payload">Payload of the message.</param>
        /// <param name="contentType">Optional content type, may be used with protocol version 
        /// MqttProtocolVersion.V500.</param>
        /// <param name="retain">True -> ratain last message with this topic on MQTT broker.</param>
        /// <param name="qosLevel">Desired Quality of Service level for transmission 
        /// client -> MQTT broker.</param>
        /// <returns>void</returns>
        public async Task PublishMessageAsync(
            string topic, 
            string payload, 
            string contentType = "", 
            bool retain = false, 
            MqttQualityOfServiceLevel qosLevel = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            Log.Here().Verbose("Publishing {topic} = '{payload}' ...", topic, payload);
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithPayloadFormatIndicator(MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData)
                .WithContentType(contentType)
                .WithRetainFlag(retain)
                .WithQualityOfServiceLevel(qosLevel)
                .Build();
            var res = await managedMqttClient.PublishAsync(mqttMessage);

            // OnMessagePublished() is called as soon as the message has been sent to the MQTT broker.
            // That's why we don't write to logger here.
        }

        /// <summary>
        /// Add a subscription for a topic. 
        /// </summary>
        /// <remarks>
        /// Any existing subscription with exactly the same topic is replaced, i.e. the 
        /// handler/callback is replaced.
        /// </remarks>
        /// <param name="topic">Topic to subscribe, may include wildcards # or +.</param>
        /// <param name="topicEventHandler">Handler/callback to be called each time a message  
        /// with this topic is received, i.e. when topic's payload has changed.</param>
        /// <param name="qosLevel">Desired Quality of Service level for transmission MQTT 
        /// broker -> client.</param>
        /// <returns>void</returns>
        public async Task AddSubscriptionAsync(
            string topic, 
            TopicChangedEventHandler topicEventHandler,
            MqttQualityOfServiceLevel qosLevel = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            Log.Here().Verbose("Adding subsription for {topic} ...");
            if (topic == null) { throw new ArgumentNullException(nameof(topic)); }
            if (topicEventHandler == null) { throw new ArgumentNullException(nameof(topicEventHandler)); }

            if (topicChangedEventHandlers.ContainsKey(topic))
            {
                topicChangedEventHandlers.Remove(topic);
                await managedMqttClient.UnsubscribeAsync(topic);
            }
            topicChangedEventHandlers.Add(topic, topicEventHandler);
            await managedMqttClient.SubscribeAsync(topic, qosLevel);
            
            Log.Here().Information("Added subscription for {topic}.", topic);
        }

        /// <summary>
        /// This handler/callback is called as soon as a pulished message has been sent to the  
        /// MQTT broker. This method just writes an information to the logger.
        /// </summary>
        /// <param name="ev">Event arguments.</param>
        private void OnMessagePublished(ApplicationMessageProcessedEventArgs ev)
        {
            var topic = ev.ApplicationMessage.ApplicationMessage.Topic;
            var binaryPayload = ev.ApplicationMessage.ApplicationMessage.Payload;
            var payload = Encoding.UTF8.GetString(binaryPayload, 0, binaryPayload.Length);
            var now = DateTime.Now.ToString("HH:mm:ss.fff");
            Log.Here().Information("Published {topic} = '{payload}'.", topic, payload);
        }

        /// <summary>
        /// This handler/callback is called every time a message is received for a subscribed topic.
        /// This method calls the handler/callback registered for this topic, <see cref="AddSubscriptionAsync()"/>.
        /// </summary>
        /// <param name="ev">Event arguments.</param>
        private void OnMessageReceived(MqttApplicationMessageReceivedEventArgs ev)
        {
            var topic = ev.ApplicationMessage.Topic;
            Log.Here().Debug("Message received for {topic} ...", topic);
            foreach (var handler in topicChangedEventHandlers)
            {
                if (IsMatch(topic, handler.Key))
                {
                    // Log the match
                    var binaryPayload = ev.ApplicationMessage.Payload;
                    var payload = Encoding.UTF8.GetString(binaryPayload, 0, binaryPayload.Length);
                    var method = handler.Value.Method;
                    var handlerName = $"{method.DeclaringType?.Name}.{method.Name}()";
                    Log.Here().Information("Received {topic} = '{payload}'. Calling handler {handler} ...", topic, payload, handlerName);

                    // Call handler
                    handler.Value(ev);
                }
            }
        }

        /// <summary>
        /// This handler/callback is called upon connection failure.This method just writes an information to the logger.
        /// </summary>
        /// <param name="ev"></param>
        private void OnConnectingFailed(ManagedProcessFailedEventArgs ev)
        {
            var msg = ev.Exception.Message;
            if (ev.Exception.InnerException != null) { msg += ": " + ev.Exception.InnerException.Message; }
            Log.Here().Error("Connection failed: {message}.", msg);
        }

        /// <summary>
        /// Define options for MQTT client.
        /// </summary>
        /// <returns>options</returns>
        private ManagedMqttClientOptions GetManagedClientOptions()
        {
            var configMqtt = Config.Instance.Mqtt;
            Log.Here().Debug("Preparing MQTT client options:");
            Log.Here().Debug("    server = {server}:{port}", configMqtt.BrokerHost, configMqtt.UseSecureConnection ? configMqtt.BrockerSecurePort : configMqtt.BrockerPort);            Log.Here().Debug("    clientId={clientId}");
            Log.Here().Debug("    use TLS = {useTls}", configMqtt.UseSecureConnection);
            Log.Here().Debug("    username = {username}", configMqtt.Username);
            Log.Here().Debug("    MQTT protocol version = {protocolVersion}", protocolVersion);
            Log.Here().Debug("    auto reconnect delay = {autoReconnectDelay} seconds", configMqtt.AutoReconnectDelay);
            Log.Here().Debug("    keep alive period = {keepAlivePeriod} seconds", configMqtt.KeepAlivePeriod);
            IMqttClientOptions clientOptions;
            if (configMqtt.UseSecureConnection)
            {
                var tlsParameters = GetTlsParameters();
                clientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(configMqtt.BrokerHost, configMqtt.BrockerSecurePort)
                    .WithClientId(configMqtt.ClientId)
                    .WithCredentials(configMqtt.Username, configMqtt.Password)
                    .WithTls(tlsParameters)
                    .WithProtocolVersion(protocolVersion)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(configMqtt.KeepAlivePeriod))
                    .Build();
            }
            else 
            {
                clientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(configMqtt.BrokerHost, configMqtt.BrockerPort)
                    .WithClientId(configMqtt.ClientId)
                    .WithCredentials(configMqtt.Username, configMqtt.Password)
                    .WithProtocolVersion(protocolVersion)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(configMqtt.KeepAlivePeriod))
                    .Build();
            }
            var managedClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions)
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(configMqtt.AutoReconnectDelay))
                .Build();
            return managedClientOptions;
        }

        /// <summary>
        ///  Define options for TLS setup. Optionally a client certificate may be specified.
        /// </summary>
        /// <returns>options</returns>
        private MqttClientOptionsBuilderTlsParameters GetTlsParameters()
        {
            var configMqtt = Config.Instance.Mqtt;
            var configCertificates = Config.Instance.Certificates;
            var tlsParameters = new MqttClientOptionsBuilderTlsParameters() {
                AllowUntrustedCertificates = false,
                UseTls = true,
                SslProtocol = System.Security.Authentication.SslProtocols.Tls13,
                CertificateValidationHandler = certificateContext => { 
                    return Certificates.ValidateServerCertificate(new X509Certificate2(certificateContext.Certificate)); 
                },
            };
            Log.Here().Debug("    use client certificate = {useClientCertificate}", configMqtt.UseClientCertificate);
            if (configMqtt.UseClientCertificate)
            {
                var caCertFilePath = Path.Combine(Directory.GetCurrentDirectory(), configCertificates.CaCertificateFilePath);
                var caCert = new X509Certificate(File.ReadAllBytes(caCertFilePath));
                var clientCertFilePath = Path.Combine(Directory.GetCurrentDirectory(), configCertificates.ClientCertificateFilePath);
                var clientCert = new X509Certificate2(File.ReadAllBytes(clientCertFilePath), configCertificates.ClientCertificatePassword);
                tlsParameters.Certificates = new List<X509Certificate>()
                {
                    clientCert, caCert
                };
            }
            return tlsParameters;
        }

        /// <summary>
        /// Check if a MQTT topic matches a topic pattern.
        /// The pattern may contain the wildcards # and +.
        /// See also https://www.hivemq.com/blog/mqtt-essentials-part-5-mqtt-topics-best-practices/
        /// </summary>
        /// <param name="topic">Topic, e.g. "inputs/button1/isPressed".</param>
        /// <param name="topicPattern">Pattern, e.g. "inputs/+/isPressed" or "inputs/#".</param>
        /// <returns>true if pattern matches topic</returns>
        private bool IsMatch(string topic, string topicPattern)
        {
            topicPattern = topicPattern.Replace("/", @"\/");
            var regexPattern = topicPattern.Replace("+", @"[^\/]*");
            regexPattern = regexPattern.Replace("#", @".*");
            var match = Regex.Match(topic, regexPattern);
            return match.Success;
        }
    }
}
