using System;

namespace Mp3Player.Audio
{
    public class FrameEventArgs : EventArgs
    {
        public float[]? Volumes { get; set; }
    }
}
