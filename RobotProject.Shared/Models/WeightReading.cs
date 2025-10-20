namespace RobotProject.Shared.Models
{
    /// <summary>
    /// Represents a weight sensor reading from the HX711
    /// </summary>
    public class WeightReading
    {
        /// <summary>
        /// Raw value from the HX711 sensor
        /// </summary>
        public long RawValue { get; set; }

        /// <summary>
        /// Weight in grams (most accurate)
        /// </summary>
        public double WeightGrams { get; set; }

        /// <summary>
        /// Weight in kilograms
        /// </summary>
        public double WeightKg { get; set; }

        /// <summary>
        /// Timestamp when the reading was taken
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Whether the reading is stable (not fluctuating significantly)
        /// </summary>
        public bool IsStable { get; set; }
    }

    /// <summary>
    /// Event arguments for weight reading events
    /// </summary>
    public class WeightReadingEventArgs : EventArgs
    {
        public WeightReading Reading { get; }

        public WeightReadingEventArgs(WeightReading reading)
        {
            Reading = reading;
        }
    }
}