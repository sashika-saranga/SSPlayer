using System;

namespace Mp3Player
{
    public class UserSettings
    {
        public float[] EqGains { get; set; } = new float[15];
        public double Volume { get; set; } = 0.8;
        public float EchoLevel { get; set; } = 0f;
        public bool EchoEnabled { get; set; } = false;
        public float ReverbLevel { get; set; } = 0f;
        public bool ReverbEnabled { get; set; } = false;
        public float StereoLevel { get; set; } = 0f;
        public bool StereoEnabled { get; set; } = false;
        public float BassLevel { get; set; } = 0f;
        public bool BassEnabled { get; set; } = false;
        public float TrebleLevel { get; set; } = 0f;
        public bool TrebleEnabled { get; set; } = false;
    }
}
