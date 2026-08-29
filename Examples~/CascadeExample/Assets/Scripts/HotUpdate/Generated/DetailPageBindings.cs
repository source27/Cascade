using Cascade.Core;
using UnityEngine.UI;

namespace Cascade.Generated
{
    /// <summary>Bindings for the Detail prefab (order: TxtTitle, BtnBack).</summary>
    public sealed class DetailPageBindings
    {
        private readonly UIBindingHost _host;

        public DetailPageBindings(UIBindingHost host)
        {
            _host = host ?? throw new global::System.ArgumentNullException(nameof(host));
        }
        public Text TxtTitle => _host.Get<Text>(0);
        public Button BtnBack => _host.Get<Button>(1);
    }
}
