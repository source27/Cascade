namespace Cascade.Core
{
    /// <summary>
    /// Read-only view of the active game flow. Register the live <see cref="GameFlow"/> as this
    /// for UI/systems that must not call transitions.
    /// </summary>
    public interface IGameFlowQuery
    {
        /// <summary>Current state id, or null before the first successful transition.</summary>
        string CurrentStateId { get; }

        /// <summary>
        /// Id of the state left by the last successful transition, or null if none
        /// (e.g. right after the first <c>RunAsync</c>).
        /// </summary>
        string PreviousStateId { get; }

        /// <summary>True when <see cref="PreviousStateId"/> is set and a return target exists.</summary>
        bool CanReturn { get; }

        bool IsTransitioning { get; }
    }
}
