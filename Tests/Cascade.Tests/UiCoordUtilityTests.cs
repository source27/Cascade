using Cascade.Core;
using NUnit.Framework;
using UnityEngine;

namespace Cascade.Tests
{
    public sealed class UICoordUtilityTests
    {
        [Test]
        public void TryWorldToScreen_InFront_ReturnsPositiveZ()
        {
            var camGo = new GameObject("cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            try
            {
                var ok = UICoordUtility.TryWorldToScreen(
                    cam,
                    Vector3.zero,
                    out var screen,
                    out var inFront);
                Assert.That(ok, Is.True);
                Assert.That(inFront, Is.True);
                Assert.That(screen.x, Is.GreaterThan(0f));
                Assert.That(screen.y, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void TryWorldToScreen_BehindCamera_InFrontFalse()
        {
            var camGo = new GameObject("cam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;
            try
            {
                var ok = UICoordUtility.TryWorldToScreen(
                    cam,
                    new Vector3(0f, 0f, -5f),
                    out _,
                    out var inFront);
                Assert.That(ok, Is.True);
                Assert.That(inFront, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void IsInsidePixelRect_RespectsCameraRect()
        {
            var camGo = new GameObject("cam");
            var cam = camGo.AddComponent<Camera>();
            cam.pixelRect = new Rect(0, 0, 100, 100);
            try
            {
                Assert.That(UICoordUtility.IsInsidePixelRect(cam, new Vector2(50, 50)), Is.True);
                Assert.That(UICoordUtility.IsInsidePixelRect(cam, new Vector2(150, 50)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
