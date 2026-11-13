using UnityEngine;

namespace CodexFramework.Gameplay
{

    //used ExplicitRK4 (the library's version probed its last stage at the half step; fixed here)
    [System.Serializable]
    public struct Spring
    {
        public float damping;
        public float mass;
        public float stiffness;
        public float startValue;
        public float endValue;
        public float initialVelocity;

        public float currentValue;
        public float currentVelocity;

        public float stepSize;
    }

    public static class SpringExtension
    {
        public const float DefaultStepSize = 1 / 60.0f;

        public static Spring Create(float value, float damping, float mass, float stiffness,
            float stepSize = DefaultStepSize) => new()
        {
            startValue = value,
            endValue = value,
            currentValue = value,
            damping = damping,
            mass = mass,
            stiffness = stiffness,
            stepSize = stepSize
        };

        public static float Evaluate(this ref Spring data, float deltaTime)
        {
            var c = data.damping;
            var m = data.mass;
            var k = data.stiffness;

            var x = data.currentValue;
            var v = data.currentVelocity;
            var end = data.endValue;

            //hack because stepsize is 0 in build somehow
            var stepSize = data.stepSize > 0 ? data.stepSize : DefaultStepSize;
            var steps = Mathf.Ceil(deltaTime / stepSize);
            for (var i = 0; i < steps; i++)
            {
                var dt = i == steps - 1 ? deltaTime - i * stepSize : stepSize;

                // Classic RK4: two probes at the half step, the last one at the full step.
                var a_v = v;
                var a_a = (-k * (x - end) - c * v) / m;

                var b_v = v + a_a * dt / 2;
                var b_a = (-k * (x + a_v * dt / 2 - end) - c * b_v) / m;

                var c_v = v + b_a * dt / 2;
                var c_a = (-k * (x + b_v * dt / 2 - end) - c * c_v) / m;

                var d_v = v + c_a * dt;
                var d_a = (-k * (x + c_v * dt - end) - c * d_v) / m;

                var dxdt = (a_v + 2 * (b_v + c_v) + d_v) / 6;
                var dvdt = (a_a + 2 * (b_a + c_a) + d_a) / 6;

                x += dxdt * dt;
                v += dvdt * dt;
            }

            data.currentValue = x;
            data.currentVelocity = v;

            return data.currentValue;
        }

        public static void UpdateEndValue(this ref Spring data, float value) =>
            data.UpdateEndValue(value, data.currentVelocity);

        public static void UpdateEndValue(this ref Spring data, float value, float velocity)
        {
            if (data.IsAtRest())
            {
                data.startValue = data.currentValue;
                data.endValue = value;
                data.initialVelocity = velocity;
            }
            else
            {
                data.endValue = value;
            }
        }

        public static void Reset(this ref Spring data)
        {
            data.currentValue = data.startValue;
            data.currentVelocity = data.initialVelocity;
        }

        public static bool IsAtRest(this ref Spring data)
        {
            const float epsilon = 0.0001f;
            return Mathf.Abs(data.currentVelocity) < epsilon;
        }

        public static void SetParams(this ref Spring data, float damping, float mass, float stiffness)
        {
            data.damping = damping;
            data.mass = mass;
            data.stiffness = stiffness;
        }

        /// <summary>
        /// Scales an impulse so it stays full until <paramref name="startRatio"/> of max stretch,
        /// then fades exponentially. <paramref name="minScale"/> keeps residual kick at the ceiling for jitter.
        /// </summary>
        public static float SoftImpulseScale(
            float currentValue, float impulse, float maxStretch, float sharpness,
            float startRatio, float minScale)
        {
            if (maxStretch <= 0f || impulse == 0f)
                return 1f;

            var stretchInKickDir = currentValue * Mathf.Sign(impulse);
            if (stretchInKickDir <= 0f)
                return 1f;

            var t = stretchInKickDir / maxStretch;
            if (t <= startRatio)
                return 1f;

            var u = (t - startRatio) / (1f - startRatio);
            return Mathf.Max(minScale, Mathf.Exp(-sharpness * u));
        }

        /// <summary>
        /// Soft ceiling: gently damp velocity that pushes further past maxStretch.
        /// Kept mild so residual impulses can still produce jitter near the limit.
        /// </summary>
        public static void ApplySoftStretchLimit(this ref Spring data, float maxStretch, float sharpness)
        {
            if (maxStretch <= 0f)
                return;

            var absX = Mathf.Abs(data.currentValue);
            if (absX <= maxStretch)
                return;

            // Only damp velocity that increases stretch
            if (data.currentValue * data.currentVelocity <= 0f)
                return;

            var overflowRatio = (absX - maxStretch) / maxStretch;
            data.currentVelocity *= Mathf.Exp(-sharpness * 0.35f * overflowRatio);
        }
    }
}
