using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cascade.Core
{
    public interface IUIPageFactory
    {
        UIBase Create(GameObject root, object bindings, object pageContext);
    }

    public sealed class UIPageRegistration
    {
        public UIPageRegistration(
            Type pageType,
            string address,
            UILayer layer,
            UIPresentation presentation,
            UICachePolicy cache,
            UIInputPolicy input,
            UIBackPolicy back,
            bool fullScreen,
            bool mask,
            bool closeOnMaskClick,
            UISafeAreaPolicy safeArea,
            string safeAreaPath,
            IUIPageFactory factory)
        {
            PageType = pageType ?? throw new ArgumentNullException(nameof(pageType));
            Address = string.IsNullOrWhiteSpace(address)
                ? throw new ArgumentException("UI address is required.", nameof(address))
                : address;
            Layer = layer;
            Presentation = presentation;
            Cache = cache;
            Input = input;
            Back = back;
            FullScreen = fullScreen;
            Mask = mask;
            CloseOnMaskClick = closeOnMaskClick;
            SafeArea = safeArea;
            SafeAreaPath = string.IsNullOrWhiteSpace(safeAreaPath) ? null : safeAreaPath;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Validate();
        }

        public Type PageType { get; }
        public string Address { get; }
        public UILayer Layer { get; }
        public UIPresentation Presentation { get; }
        public UICachePolicy Cache { get; }
        public UIInputPolicy Input { get; }
        public UIBackPolicy Back { get; }
        public bool FullScreen { get; }
        public bool Mask { get; }
        public bool CloseOnMaskClick { get; }
        public UISafeAreaPolicy SafeArea { get; }
        public string SafeAreaPath { get; }
        public IUIPageFactory Factory { get; }

        private void Validate()
        {
            if (Presentation == UIPresentation.Screen)
            {
                if (!FullScreen)
                    throw new InvalidOperationException($"Screen pages must be FullScreen: {PageType.FullName}");
                if (Mask)
                    throw new InvalidOperationException($"Screen pages cannot use Mask: {PageType.FullName}");
            }

            if (Presentation == UIPresentation.Overlay || Presentation == UIPresentation.WorldWidget)
            {
                if (FullScreen)
                    throw new InvalidOperationException($"{Presentation} pages cannot be FullScreen: {PageType.FullName}");
                if (Mask)
                    throw new InvalidOperationException($"{Presentation} pages cannot use Mask: {PageType.FullName}");
            }

            if (FullScreen && Mask)
                throw new InvalidOperationException($"FullScreen pages cannot use Mask: {PageType.FullName}");

            if (Mask && Input != UIInputPolicy.Block)
                throw new InvalidOperationException($"Mask requires Input.Block: {PageType.FullName}");

            if (CloseOnMaskClick && (Presentation != UIPresentation.Modal || Input != UIInputPolicy.Block))
                throw new InvalidOperationException($"CloseOnMaskClick requires Modal + Block: {PageType.FullName}");

            if (Presentation == UIPresentation.WorldWidget && SafeArea != UISafeAreaPolicy.None)
                throw new InvalidOperationException($"WorldWidget cannot use SafeArea: {PageType.FullName}");

            if ((SafeArea & UISafeAreaPolicy.TopMask) != 0 &&
                (SafeArea & UISafeAreaPolicy.FitTop) == 0)
                throw new InvalidOperationException($"TopMask requires FitTop: {PageType.FullName}");

            if ((SafeArea & UISafeAreaPolicy.BottomMask) != 0 &&
                (SafeArea & UISafeAreaPolicy.FitBottom) == 0)
                throw new InvalidOperationException($"BottomMask requires FitBottom: {PageType.FullName}");
        }
    }

    public sealed class UIRegistry
    {
        private readonly Dictionary<Type, UIPageRegistration> _registrations =
            new Dictionary<Type, UIPageRegistration>();

        public int Count => _registrations.Count;

        public void Register(UIPageRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));
            if (!typeof(UIBase).IsAssignableFrom(registration.PageType))
                throw new ArgumentException("UI page must inherit UIBase.", nameof(registration));
            if (_registrations.ContainsKey(registration.PageType))
                throw new InvalidOperationException($"UI page already registered: {registration.PageType.FullName}");

            _registrations.Add(registration.PageType, registration);
        }

        public UIPageRegistration Get<TUI>() where TUI : UIBase
        {
            return Get(typeof(TUI));
        }

        public UIPageRegistration Get(Type pageType)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));
            if (!_registrations.TryGetValue(pageType, out var registration))
                throw new InvalidOperationException($"UI page is not registered: {pageType.FullName}");
            return registration;
        }
    }
}
