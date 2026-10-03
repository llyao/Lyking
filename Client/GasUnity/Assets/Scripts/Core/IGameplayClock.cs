using System;

namespace Lyking.GameplayAbilities.Core
{
    public interface IGameplayClock
    {
        double Now { get; }

        int Schedule(double delaySeconds, Action callback);

        void Cancel(int timerId);
    }
}
