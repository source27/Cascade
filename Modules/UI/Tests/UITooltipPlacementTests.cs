using Cascade.Modules.UI;
using NUnit.Framework;
using UnityEngine;

namespace Cascade.Modules.UI.Tests
{
    public sealed class UITooltipPlacementTests
    {
        private static readonly Rect Viewport = new Rect(0f, 0f, 1000f, 800f);
        private static readonly UITooltipPlacementOptions Options =
            new UITooltipPlacementOptions(20f, 10f, 30f);

        [Test]
        public void CenterAnchor_PrefersAbove_WithBottomArrow()
        {
            var result = Place(new Rect(450f, 350f, 100f, 40f));

            Assert.That(result.ArrowEdge, Is.EqualTo(UITooltipArrowEdge.Bottom));
            Assert.That(result.BubbleRect.yMin, Is.EqualTo(400f));
            Assert.That(result.ArrowOffset, Is.EqualTo(150f));
        }

        [Test]
        public void TopAnchor_FlipsBelow_WithTopArrow()
        {
            var result = Place(new Rect(450f, 730f, 100f, 40f));

            Assert.That(result.ArrowEdge, Is.EqualTo(UITooltipArrowEdge.Top));
            Assert.That(result.BubbleRect.yMax, Is.EqualTo(720f));
        }

        [Test]
        public void LeftAnchor_ClampsBubble_AndMovesArrowLeft()
        {
            var result = Place(new Rect(0f, 350f, 40f, 40f));

            Assert.That(result.BubbleRect.xMin, Is.EqualTo(20f));
            Assert.That(result.ArrowOffset, Is.EqualTo(30f));
        }

        [Test]
        public void OversizedTooltip_StaysInsideViewport()
        {
            var result = UITooltipPlacement.Place(
                Viewport,
                new UITooltipAnchor(new Rect(450f, 350f, 100f, 40f), new Vector2(500f, 370f)),
                new Vector2(2000f, 2000f),
                Options);

            Assert.That(result.BubbleRect.xMin, Is.EqualTo(20f));
            Assert.That(result.BubbleRect.xMax, Is.EqualTo(980f));
            Assert.That(result.BubbleRect.yMin, Is.EqualTo(20f));
            Assert.That(result.BubbleRect.yMax, Is.EqualTo(780f));
        }

        private static UITooltipPlacementResult Place(Rect anchor)
        {
            return UITooltipPlacement.Place(
                Viewport,
                new UITooltipAnchor(anchor, anchor.center),
                new Vector2(300f, 180f),
                Options);
        }
    }
}
