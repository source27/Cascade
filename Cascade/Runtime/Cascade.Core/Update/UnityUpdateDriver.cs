using UnityEngine;

namespace Cascade.Core
{
    public sealed class UnityUpdateDriver : MonoBehaviour
    {
        private IUpdateLoop _loop;

        public IUpdateLoop Loop => _loop;

        public void Bind(IUpdateLoop loop)
        {
            _loop = loop ?? throw new global::System.ArgumentNullException(nameof(loop));
        }

        private void Update()
        {
            _loop?.TickUpdate();
        }

        private void LateUpdate()
        {
            _loop?.TickLateUpdate();
        }

        private void FixedUpdate()
        {
            _loop?.TickFixedUpdate();
        }
    }
}
