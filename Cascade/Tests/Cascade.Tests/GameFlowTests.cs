using System;
using System.Collections.Generic;
using System.Threading;
using Cascade.Core;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Cascade.Tests
{
    public sealed class GameFlowTests
    {
        [Test]
        public void RunAsync_EntersInitialState()
        {
            var main = new RecordingState("Main");
            var flow = new GameFlow(new IGameFlowState[] { main });

            flow.RunAsync("Main").GetAwaiter().GetResult();

            Assert.That(flow.CurrentStateId, Is.EqualTo("Main"));
            Assert.That(main.EnterCount, Is.EqualTo(1));
            Assert.That(main.ExitCount, Is.EqualTo(0));
            Assert.That(main.Order, Is.EqualTo(new[] { "Main.enter" }));
        }

        [Test]
        public void ChangeState_ExitsPrevious_ThenEntersNext()
        {
            var order = new List<string>();
            var main = new RecordingState("Main", order);
            var battle = new RecordingState("Battle", order);
            var flow = new GameFlow(new IGameFlowState[] { main, battle });

            flow.RunAsync("Main").GetAwaiter().GetResult();
            flow.ChangeStateAsync("Battle").GetAwaiter().GetResult();

            Assert.That(flow.CurrentStateId, Is.EqualTo("Battle"));
            Assert.That(flow.PreviousStateId, Is.EqualTo("Main"));
            Assert.That(flow.CanReturn, Is.True);
            Assert.That(order, Is.EqualTo(new[] { "Main.enter", "Main.exit", "Battle.enter" }));
        }

        [Test]
        public void ChangeState_SameId_IsNoOp()
        {
            var main = new RecordingState("Main");
            var flow = new GameFlow(new IGameFlowState[] { main });
            flow.RunAsync("Main").GetAwaiter().GetResult();

            flow.ChangeStateAsync("Main").GetAwaiter().GetResult();

            Assert.That(main.EnterCount, Is.EqualTo(1));
            Assert.That(main.ExitCount, Is.EqualTo(0));
        }

        [Test]
        public void UnknownState_Throws()
        {
            var flow = new GameFlow(new IGameFlowState[] { new RecordingState("Main") });
            Assert.Throws<InvalidOperationException>(() =>
                flow.ChangeStateAsync("Nope").GetAwaiter().GetResult());
        }

        [Test]
        public void DuplicateStateId_ThrowsAtConstruction()
        {
            Assert.Throws<ArgumentException>(() =>
                new GameFlow(new IGameFlowState[]
                {
                    new RecordingState("Main"),
                    new RecordingState("Main")
                }));
        }

        [Test]
        public void EmptyStates_ThrowsAtConstruction()
        {
            Assert.Throws<ArgumentException>(() =>
                new GameFlow(Array.Empty<IGameFlowState>()));
        }

        [Test]
        public void ReturnAsync_GoesToPrevious_AndSwapsSlot()
        {
            var order = new List<string>();
            var main = new RecordingState("Main", order);
            var battle = new RecordingState("Battle", order);
            var flow = new GameFlow(new IGameFlowState[] { main, battle });

            flow.RunAsync("Main").GetAwaiter().GetResult();
            Assert.That(flow.PreviousStateId, Is.Null);
            Assert.That(flow.CanReturn, Is.False);

            flow.ChangeStateAsync("Battle").GetAwaiter().GetResult();
            flow.ReturnAsync().GetAwaiter().GetResult();

            Assert.That(flow.CurrentStateId, Is.EqualTo("Main"));
            Assert.That(flow.PreviousStateId, Is.EqualTo("Battle"));
            Assert.That(order, Is.EqualTo(new[]
            {
                "Main.enter",
                "Main.exit",
                "Battle.enter",
                "Battle.exit",
                "Main.enter"
            }));
        }

        [Test]
        public void ReturnAsync_WithoutPrevious_Throws()
        {
            var flow = new GameFlow(new IGameFlowState[] { new RecordingState("Main") });
            flow.RunAsync("Main").GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() =>
                flow.ReturnAsync().GetAwaiter().GetResult());
        }


        private sealed class RecordingState : IGameFlowState
        {
            private readonly List<string> _order;

            public RecordingState(string id, List<string> order = null)
            {
                Id = id;
                _order = order ?? new List<string>();
            }

            public string Id { get; }
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }
            public IReadOnlyList<string> Order => _order;

            public UniTask EnterAsync(CancellationToken cancellationToken = default)
            {
                EnterCount++;
                _order.Add(Id + ".enter");
                return UniTask.CompletedTask;
            }

            public UniTask ExitAsync(CancellationToken cancellationToken = default)
            {
                ExitCount++;
                _order.Add(Id + ".exit");
                return UniTask.CompletedTask;
            }
        }
    }
}
