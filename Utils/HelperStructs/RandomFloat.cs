using System;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct RandomFloat
    {
        public float Min;
        public float Max;

        public float Value => UnityEngine.Random.Range(Min, Max);
        public float Average
        {
            get => (Min + Max) / 2;
            set
            {
                var delta = Distance / 2;
                Min = value - delta;
                Max = value + delta;
            }
        }
        public float Distance => Max - Min;
    }
}