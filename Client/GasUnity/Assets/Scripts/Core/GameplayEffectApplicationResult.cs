#nullable enable
namespace Lyking.GameplayAbilities.Core
{
    public enum GameplayEffectFailureReason
    {
        None,
        InvalidSpec,
        InvalidAttribute,
        InvalidValue,
        SchedulingFailed,
        ApplicationInProgress,
    }

    public readonly struct GameplayEffectApplicationResult
    {
        private GameplayEffectApplicationResult(
            bool succeeded,
            GameplayEffectFailureReason reason,
            ActiveGameplayEffectHandle handle)
        {
            Succeeded = succeeded;
            Reason = reason;
            Handle = handle;
        }

        public bool Succeeded { get; }

        public GameplayEffectFailureReason Reason { get; }

        public ActiveGameplayEffectHandle Handle { get; }

        public static GameplayEffectApplicationResult SucceededWith(ActiveGameplayEffectHandle handle)
        {
            return new GameplayEffectApplicationResult(true, GameplayEffectFailureReason.None, handle);
        }

        public static GameplayEffectApplicationResult Failed(GameplayEffectFailureReason reason)
        {
            return new GameplayEffectApplicationResult(false, reason, default);
        }
    }
}
