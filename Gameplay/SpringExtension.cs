using UnityEngine;

namespace CodexFramework.Gameplay
{

    //used ExplicitRK4
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
        public static float Evaluate(this ref Spring data, float deltaTime)
        {
            var c = data.damping;
            var m = data.mass;
            var k = data.stiffness;

            var x = data.currentValue;
            var v = data.currentVelocity;
            var _x = data.currentValue;
            var _v = data.currentVelocity;

            var steps = Mathf.Ceil(deltaTime / data.stepSize);
            for (var i = 0; i < steps; i++)
            {
                var dt = i == steps - 1 ? deltaTime - i * data.stepSize : data.stepSize;

                var a_v = _v;
                var a_a = (-k * (_x - data.endValue) - c * _v) / m;
                _x = x + a_v * dt / 2;
                _v = v + a_a * dt / 2;

                var b_v = _v;
                var b_a = (-k * (_x - data.endValue) - c * _v) / m;
                _x = x + b_v * dt / 2;
                _v = v + b_a * dt / 2;

                var c_v = _v;
                var c_a = (-k * (_x - data.endValue) - c * _v) / m;
                _x = x + c_v * dt / 2;
                _v = v + c_a * dt / 2;

                var d_v = _v;
                var d_a = (-k * (_x - data.endValue) - c * _v) / m;
                _x = x + c_v * dt / 2;
                _v = v + c_a * dt / 2;

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
            return data.currentVelocity < epsilon;
        }
    }
}