using System;

namespace FinalLab.Investigation
{
    public sealed class InvestigationProgress
    {
        public int Count { get; private set; }
        public int Total { get; }

        public InvestigationProgress(int total)
        {
            if (total < 1) throw new ArgumentOutOfRangeException(nameof(total));
            Total = total;
        }

        public bool IsStudied(int index) => index >= 0 && index < Count;

        public bool Complete(int index)
        {
            if (index != Count || Count == Total) return false;
            Count++;
            return true;
        }
    }
}
