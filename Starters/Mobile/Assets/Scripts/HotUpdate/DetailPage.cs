using Cascade.Core;
using Cascade.Generated;

namespace GameLogic
{
    [UI(Address = "Detail", Layer = UILayer.Top, Presentation = UIPresentation.Modal, CloseOnMaskClick = true)]
    public sealed class DetailPage : UIPage<DetailPage.Args, DetailPageBindings>
    {
        public struct Args : IUIArgs<DetailPage>
        {
        }

        protected override void OnPageCreate()
        {
            Bindings.TxtTitle.text = "Detail page — bindings via UIBindingHost";
            Bindings.BtnBack.onClick.AddListener(() => Ctx.UI.Back());
        }
    }
}
