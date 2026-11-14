using UnityEngine;

namespace Astra.UI
{
    /// <summary>
    /// The academy's sound cues (TASKS S12). Create via Assets → Create → ASTRA → Audio Cues, or
    /// ASTRA → P1 → Create AudioCues asset (fills it from Assets/ASTRA/Audio). The team lead hooks playback into the
    /// factory and LevelController. Clips are self-made (see Assets/ASTRA/Audio/CREDITS.md), short, about −14 LUFS;
    /// the hum loops quietly.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCues", menuName = "ASTRA/Audio Cues")]
    public sealed class AudioCues : ScriptableObject
    {
        public AudioClip hover;
        public AudioClip select;
        public AudioClip toggle;
        public AudioClip button;
        public AudioClip dialTick;
        public AudioClip grab;
        public AudioClip place;
        public AudioClip correct;
        public AudioClip wrong;
        public AudioClip phaseChange;
        public AudioClip alarm;
        public AudioClip success;
        public AudioClip fail;
        public AudioClip ambientHum;

        /// <summary>Plays a one-shot cue on <paramref name="source"/> (null-safe).</summary>
        public static void Play(AudioSource source, AudioClip clip, float volume = 1f)
        {
            if (source != null && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
