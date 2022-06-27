namespace Iot.Raspi.Mqtt
{
    public class ConfigOutputs
    {
        /// <summary>Array of GPIO pin numbers to which the LEDs are connected. Any number of LEDs are supported.</summary>
        public int[] LedPins { get; set; } = new int[] {};
    }
}