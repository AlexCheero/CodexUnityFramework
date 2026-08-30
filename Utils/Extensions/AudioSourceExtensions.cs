using UnityEngine;

namespace CodexFramework.Utils
{
    public static class AudioSourceExtensions
    {
        public static void PlayOneShotRandomPitch(this AudioSource audioSource, AudioClip clip, float minPitch = 0.9f, float maxPitch = 1.1f)
        {
            audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(clip);
        }
    }
}