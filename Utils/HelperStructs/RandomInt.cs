using System;

namespace CodexFramework.Utils
{
    [Serializable]
    public struct RandomInt
    {
        public int Min;
        public int Max;

        public int Value => UnityEngine.Random.Range(Min, Max);
        public int Average
        {
            get => (Min + Max) / 2;
            set
            {
                var delta = Distance / 2;
                Min = value - delta;
                Max = value + delta;
            }
        }
        public int Distance => Max - Min;
    }
}