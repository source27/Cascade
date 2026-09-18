using Cascade.Modules.UI;
using UnityEngine.UI;

namespace Cascade.Generated
{
    /// <summary>
    /// Hand-written stand-in for the Cascade.Editor "Generate UI Page" output.
    /// Index order must match the UIBindingHost object order on the Home prefab
    /// (see ExampleSetup: BtnLocale, BtnSave, BtnLoad, BtnAudio, BtnDetail,
    /// BtnBack, TxtStatus, TxtCounter, TxtTitle).
    /// </summary>
    public sealed class HomePageBindings
    {
        private readonly UIBindingHost _host;

        public HomePageBindings(UIBindingHost host)
        {
            _host = host ?? throw new global::System.ArgumentNullException(nameof(host));
        }
        public Button BtnLocale => _host.Get<Button>(0);
        public Button BtnSave => _host.Get<Button>(1);
        public Button BtnLoad => _host.Get<Button>(2);
        public Button BtnAudio => _host.Get<Button>(3);
        public Button BtnDetail => _host.Get<Button>(4);
        public Button BtnBack => _host.Get<Button>(5);
        public Text TxtStatus => _host.Get<Text>(6);
        public Text TxtCounter => _host.Get<Text>(7);
        public Text TxtTitle => _host.Get<Text>(8);
    }
}
