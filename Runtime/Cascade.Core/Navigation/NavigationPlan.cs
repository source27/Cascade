using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Cascade.Core
{
    public enum NavigationStepKind
    {
        Context,
        Scene,
        Mode,
        UI,
        Guide
    }

    public readonly struct NavigationStep
    {
        public NavigationStep(NavigationStepKind kind, string target, object arguments = null)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Navigation target is required.", nameof(target));
            Kind = kind;
            Target = target;
            Arguments = arguments;
        }

        public NavigationStepKind Kind { get; }
        public string Target { get; }
        public object Arguments { get; }
    }

    public sealed class NavigationPlan
    {
        private readonly NavigationStep[] _steps;

        public NavigationPlan(IEnumerable<NavigationStep> steps)
        {
            if (steps == null)
                throw new ArgumentNullException(nameof(steps));
            _steps = new List<NavigationStep>(steps).ToArray();
        }

        public IReadOnlyList<NavigationStep> Steps => _steps;
    }

    public interface INavigationStepExecutor
    {
        UniTask ExecuteAsync(NavigationStep step, CancellationToken cancellationToken = default);
    }

    public sealed class NavigationPlanExecutor
    {
        public async UniTask ExecuteAsync(
            NavigationPlan plan,
            INavigationStepExecutor executor,
            CancellationToken cancellationToken = default)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            for (var i = 0; i < plan.Steps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await executor.ExecuteAsync(plan.Steps[i], cancellationToken);
            }
        }
    }
}
