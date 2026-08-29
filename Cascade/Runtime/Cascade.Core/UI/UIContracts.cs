using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Core
{
    public enum UILayer
    {
        Normal,
        Top,
        World
    }

    public enum UIPresentation
    {
        Screen,
        Modal,
        Overlay,
        WorldWidget
    }

    public enum UICachePolicy
    {
        Auto,
        KeepAlive,
        None
    }

    public enum UIInputPolicy
    {
        PassThrough,
        Block
    }

    public enum UIBackPolicy
    {
        Close = 0,
        Block = 1
    }

    public enum UIContextId
    {
        Global,
        Main,
        MiniGame
    }

    public enum UIPageState
    {
        Created,
        Active,
        Hidden,
        CachedHidden,
        Destroyed
    }

    [Flags]
    public enum UISafeAreaPolicy
    {
        None = 0,
        FitTop = 1 << 0,
        FitBottom = 1 << 1,
        TopMask = 1 << 2,
        BottomMask = 1 << 3
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIAttribute : Attribute
    {
        public UILayer Layer { get; set; } = UILayer.Normal;
        public UIPresentation Presentation { get; set; } = UIPresentation.Screen;
        public UICachePolicy Cache { get; set; } = UICachePolicy.Auto;
        public UIInputPolicy Input { get; set; } = UIInputPolicy.PassThrough;
        public UIBackPolicy Back { get; set; } = UIBackPolicy.Close;
        public bool FullScreen { get; set; } = true;
        public bool Mask { get; set; }
        public bool CloseOnMaskClick { get; set; }
        public UISafeAreaPolicy SafeArea { get; set; } = UISafeAreaPolicy.FitTop;
        public string SafeAreaPath { get; set; }
        public string Address { get; set; }
    }

    public interface IUIArgs<out TUI> where TUI : UIBase
    {
    }

    public interface IUISystem : IDisposable
    {
        UIContextId? ActiveContext { get; }
        void SetActiveContext(UIContextId context);

        /// <summary>World-tracked UI host for the given context (Main/MiniGame). Defaults to ActiveContext.</summary>
        IWorldUIHost GetWorldUIHost(UIContextId? context = null);

        UniTask<TUI> OpenUI<TUI>(
            IUIArgs<TUI> args,
            CancellationToken cancellationToken = default)
            where TUI : UIBase;

        UniTask<TUI> OpenUI<TUI>(
            IUIArgs<TUI> args,
            UIContextId context,
            CancellationToken cancellationToken = default)
            where TUI : UIBase;

        UniTask<TUI> ReplaceUI<TUI>(
            IUIArgs<TUI> args,
            CancellationToken cancellationToken = default)
            where TUI : UIBase;

        UniTask<TUI> ReplaceUI<TUI>(
            IUIArgs<TUI> args,
            UIContextId context,
            CancellationToken cancellationToken = default)
            where TUI : UIBase;

        bool CloseUI<TUI>() where TUI : UIBase;
        bool CloseUI<TUI>(UIContextId context) where TUI : UIBase;
        bool Back();
        bool Back(UIContextId context);
        void CloseAll(UIContextId context);
        bool IsOpen<TUI>() where TUI : UIBase;
        bool IsOpen<TUI>(UIContextId context) where TUI : UIBase;
        void BindPageContext(object context);
        void ResetPageContext();
    }
}
