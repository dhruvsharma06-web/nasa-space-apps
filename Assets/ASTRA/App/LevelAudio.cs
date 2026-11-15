using Astra.UI;
using UnityEngine;

namespace Astra.App
{
    /// <summary>
    /// One shared 2D audio source per level for UI/feedback cues (AudioCues from XR &amp; Interaction), plus the
    /// quiet looping cabin hum. Every call is safe when no cues are assigned.
    /// OWNER: Integration Lead.
    /// </summary>
    public static class LevelAudio
    {
        public static AudioCues Cues { get; private set; }
        private static AudioSource _source;

        public static void Init(GameObject host, AudioCues cues)
        {
            Cues = cues;
            _source = host.GetComponent<AudioSource>();
            if (_source == null) _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false; _source.spatialBlend = 0f;
            if (cues != null && cues.ambientHum != null)
            {
                var hum = host.AddComponent<AudioSource>();
                hum.clip = cues.ambientHum; hum.loop = true; hum.volume = 0.12f; hum.spatialBlend = 0f; hum.Play();
            }
        }

        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip, volume);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Cues = null; _source = null; }
    }
}
