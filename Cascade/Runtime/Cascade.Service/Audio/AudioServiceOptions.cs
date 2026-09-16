using System;
using UnityEngine.Audio;

namespace Cascade.Service
{
    /// <summary>
    /// Tunables for <see cref="AudioService"/> one-shot budget, pool, and mixer routing.
    /// Defaults match Cascade Audio P0 ship values.
    /// </summary>
    public sealed class AudioServiceOptions
    {
        /// <summary>Max simultaneous one-shot SFX voices. Default 24.</summary>
        public int MaxConcurrentOneShots { get; set; } = 24;

        /// <summary>Max simultaneous one-shots sharing the same location key. Default 3.</summary>
        public int MaxInstancesPerKey { get; set; } = 3;

        /// <summary>
        /// Minimum seconds between accepted starts for the same location key.
        /// Default 0.05 (50 ms). Zero disables cooldown.
        /// </summary>
        public float PerKeyCooldownSeconds { get; set; } = 0.05f;

        /// <summary>Hard max SFX entities (active + idle). Default 32.</summary>
        public int MaxPoolSize { get; set; } = 32;

        /// <summary>Idle SFX sources to create at construction. Clamped to MaxPoolSize. Default 0.</summary>
        public int PrewarmCount { get; set; } = 0;

        public AudioMixerGroup MusicGroup { get; set; }
        public AudioMixerGroup SfxGroup { get; set; }
        public AudioMixerGroup VoiceGroup { get; set; }

        /// <summary>
        /// Clock for cooldown and one-shot lifetime. Defaults to <c>Time.unscaledTime</c>.
        /// Tests may replace with a controllable clock.
        /// </summary>
        public Func<float> UnscaledTime { get; set; }
    }
}
