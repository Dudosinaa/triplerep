using System;

namespace FinalLab.Investigation
{
    public sealed class InvestigationProgress
    {
        private readonly bool[] studied;
        public int Count { get; private set; }
        public int Total => studied.Length;

        public InvestigationProgress(int total)
        {
            if (total < 1) throw new ArgumentOutOfRangeException(nameof(total));
            studied = new bool[total];
        }

        public bool IsStudied(int index) => studied[index];

        public bool Complete(int index)
        {
            if (studied[index]) return false;
            studied[index] = true;
            while (Count < Total && studied[Count]) Count++;
            return true;
        }
    }
}
