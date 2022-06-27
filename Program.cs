using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Device.Gpio;
using MQTTnet;

using Iot.Raspi.Logger;

namespace Iot.Raspi.Mqtt
{
    /// <summary>
    /// SUBSCRIBE FROM MQTT BROCKER:
    /// The program subscribes for changes of the two topicsinputs/button{buttonIndex}/isPressed and 
    /// inputs/button{buttonIndex}/lastChangedAt. The topic change handlers write to logger (console 
    /// and/or log file).
    /// </summary>
    class Program
    {
        // Set logger context
        private static Serilog.ILogger Log => Serilog.Log.ForContext<Program>();

        private static GpioController? gpio;
        private static MqttClient? mqttClient;

        // Entry point of program
        static async Task Main(string[] args)
        {
            try 
            {
                // Initialize logger (Serilog), see appsettings.json for configuration.
                // Here() is an extension method to Serilog.ILogger, it automatically extends the logger 
                // context with information about class name, method name, source file path and 
                // line number. This information may be written to log outputs (see configuration).
                IotLogger.Init("appsettings.json");
                Log.Here().Information("Application starting ...");

                // Configure GPIO digital inputs
                gpio = new GpioController();
                foreach (var ledPin in Config.Instance.Outputs.LedPins)
                {
                    gpio.OpenPin(ledPin, PinMode.Output);
                    gpio.Write(ledPin, PinValue.Low);
                }

                // Configure MQTT client
                mqttClient = new MqttClient();
                await mqttClient.AddSubscriptionAsync("inputs/+/isPressed", OnButtonStateChanged);
                await mqttClient.AddSubscriptionAsync("inputs/#", OnButtonChanged);
                await mqttClient.StartAsync();
                
                // Do nothing
                while (true)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Log.Here().Fatal("Failed: {exception}", ex.Message );
            }
            finally
            {
                Serilog.Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// This handler is called when topic "inputs/+/lastChangedAt" has been changed.
        /// </summary>
        /// <param name="ev">Event arguments.</param>
        private static void OnButtonStateChanged(MqttApplicationMessageReceivedEventArgs ev)
        {
            if (gpio == null) { throw new Exception("Failed to initialize GPIO."); }

            Log.Here().Information("Topic has changed: {topic} = '{payload}'.", 
                ev.ApplicationMessage.Topic, 
                Encoding.UTF8.GetString(ev.ApplicationMessage.Payload));

            var topic = ev.ApplicationMessage.Topic;
            var buttonIndex = GetButtonIndexFromTopic(topic);
            var ledPin = Config.Instance.Outputs.LedPins[buttonIndex];
            var payload = Encoding.UTF8.GetString(ev.ApplicationMessage.Payload);
            var ledValue = String.Compare(payload, "true", true) == 0 ? PinValue.High : PinValue.Low;
            gpio.Write(ledPin, ledValue);
        }

        /// <summary>
        /// This handler is called when topic "inputs/#" has been changed.
        /// </summary>
        /// <param name="ev">Event arguments.</param>
        private static void OnButtonChanged(MqttApplicationMessageReceivedEventArgs ev)
        {
            Log.Here().Information("Topic has changed: {topic} = '{payload}'.", 
                ev.ApplicationMessage.Topic, 
                Encoding.UTF8.GetString(ev.ApplicationMessage.Payload));
        }

        /// <summary>
        /// Get button index.  For topic like "inputs/button1/isPressed" return 1.
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        private static int GetButtonIndexFromTopic(string topic)
        {
            var index = -1;
            var pattern = "inputs\\/button(\\d*)\\/isPressed";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(topic);
            if (match.Groups.Count == 2)
            {
                int.TryParse(match.Groups[1].Captures[0].Value, out index);
            }
            return index;
        }
    }
}