using System;
using System.Threading;
using Cascade.Core;

namespace CascadeExample
{
    /// <summary>
    /// Consumer-side reusable view base class (pattern from the retired client
    /// project). Views are page-owned widgets bound from a page's Bindings.
    /// </summary>
    public abstract class UIView<TModel, TBindings> : IUIView
        where TBindings : class
    {
        protected UIView(TBindings bindings, PageContext context)
        {
            Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected TBindings Bindings { get; }
        protected PageContext Context { get; }

        public void Bind(TModel model, CancellationToken cancellationToken = default)
        {
            OnBind(model, cancellationToken);
        }

        protected abstract void OnBind(TModel model, CancellationToken cancellationToken);

        public virtual void Clear()
        {
        }

        public virtual void Dispose()
        {
        }
    }
}
