using System;
using Cascade.Modules.UI;

namespace GameLogic
{
    /// <summary>
    /// Consumer-side page base class (pattern from the retired client project).
    /// Pages are plain C# classes decorated with [UI]; the Cascade source generator
    /// emits the explicit registry + factories (UIRegistryGenerated) at compile time.
    /// </summary>
    public abstract class UIPage<TArgs, TBindings> : UIBase<TArgs>
        where TBindings : class
    {
        protected new PageContext Ctx =>
            base.Ctx as PageContext
            ?? throw new InvalidOperationException(
                $"UI page context is not available or has the wrong type. Expected {typeof(PageContext).FullName}.");

        protected new TBindings Bindings =>
            base.Bindings as TBindings
            ?? throw new InvalidOperationException(
                $"UI bindings are not available or have the wrong type. Expected {typeof(TBindings).FullName}.");

        protected sealed override void OnCreate()
        {
            _ = Ctx;
            _ = Bindings;
            OnPageCreate();
        }

        protected sealed override void OnOpen(TArgs args) => OnPageOpen(args);
        protected sealed override void OnShow() => OnPageShow();
        protected sealed override void OnHide() => OnPageHide();
        protected sealed override void OnClose() => OnPageClose();
        protected sealed override void OnRemove() => OnPageRemove();

        protected virtual void OnPageCreate()
        {
        }

        protected virtual void OnPageOpen(TArgs args)
        {
        }

        protected virtual void OnPageShow()
        {
        }

        protected virtual void OnPageHide()
        {
        }

        protected virtual void OnPageClose()
        {
        }

        protected virtual void OnPageRemove()
        {
        }
    }
}
