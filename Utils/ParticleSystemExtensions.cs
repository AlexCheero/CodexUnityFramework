using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.Utils
{
    public static class ParticleSystemExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Restart(this ParticleSystem particleSystem, bool withChildren = true)
        {
            particleSystem.Stop(withChildren, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(withChildren);
        }
        
        public static float GetTotalDuration(this ParticleSystem particleSystem)
        {
            float GetDuration(ParticleSystem ps)
            {
                var main = ps.main;
                var delay = 0f;
                if (main.startDelay.mode == ParticleSystemCurveMode.TwoConstants)
                    delay = main.startDelay.constantMax;
                else if (main.startDelay.mode == ParticleSystemCurveMode.Constant)
                    delay = main.startDelay.constant;

                var lifetime = 0f;
                if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                    lifetime = main.startLifetime.constantMax;
                else if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                    lifetime = main.startLifetime.constant;

                return delay + main.duration + lifetime;
            }

            var maxDuration = 0.0f;
            foreach (var ps in particleSystem.GetComponentsInChildren<ParticleSystem>())
            {
                var duration = GetDuration(ps);
                if (maxDuration < duration)
                    maxDuration = duration;
            }

            return maxDuration;
        }
    }
}