using UnityEngine;

namespace Cascade.Core
{
    public sealed class UnityUpdateDriver : MonoBehaviour
    {
        private UpdateLoop _loop;

        public UpdateLoop Loop => _loop;

        public void Bind(UpdateLoop loop)
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
