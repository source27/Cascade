using System;
using System.Threading;
using Cascade.Modules.UI;
using Cascade.Generated;
using Cascade.Service;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [UI(Address = "Home", Layer = UILayer.Normal, Presentation = UIPresentation.Screen)]
    public sealed class HomePage : UIPage<HomePage.Args, HomePageBindings>
    {
        public struct Args : IUIArgs<HomePage>
        {
        }

        private float _counter;
        private float _lastDisplayedCounter;
        private IDisposable _updateRegistration;
        private bool _localeZh;

        protected override void OnPageCreate()
        {
            Bindings.BtnLocale.onClick.AddListener(OnLocaleClicked);
            Bindings.BtnSave.onClick.AddListener(OnSaveClicked);
            Bindings.BtnLoad.onClick.AddListener(OnLoadClicked);
            Bindings.BtnAudio.onClick.AddListener(OnAudioClicked);
            Bindings.BtnDetail.onClick.AddListener(OnDetailClicked);
            Bindings.BtnBack.onClick.AddListener(OnBackClicked);
            RefreshTexts();
        }

        protected override void OnPageShow()
        {
            _updateRegistration = Ctx.UpdateLoop.RegisterUpdate(OnUpdate);
        }

        protected override void OnPageHide()
        {
            _updateRegistration?.Dispose();
            _updateRegistration = null;
        }

        private void OnUpdate()
        {
            _counter += Time.deltaTime;
            if (_counter - _lastDisplayedCounter >= 1f)
            {
                _lastDisplayedCounter = _counter;
                Bindings.TxtCounter.text = $"{Get("home.counter")}: {_lastDisplayedCounter:0.0}s";
            }
        }

        private async void OnLocaleClicked()
        {
            _localeZh = !_localeZh;
            await Ctx.Services.Get<ILocalizationService>().SetLocaleAsync(_localeZh ? "zh" : "en");
            RefreshTexts();
        }

        private void OnSaveClicked()
        {
            Ctx.Services.Get<ISaveService>().SetString("demo", DateTime.Now.ToString("HH:mm:ss"));
            Bindings.TxtStatus.text = $"Saved: {Ctx.Services.Get<ISaveService>().GetString("demo")}";
        }

        private void OnLoadClicked()
        {
            Bindings.TxtStatus.text = $"Loaded: {Ctx.Services.Get<ISaveService>().GetString("demo", "(none)")}";
        }

        private async void OnAudioClicked()
        {
            try
            {
                var duration = await Ctx.Services.Get<IAudioService>().PlayOneShotAsync("SfxClick");
                Bindings.TxtStatus.text = $"Sfx played ({duration:0.00}s).";
            }
            catch (Exception exception)
            {
                Bindings.TxtStatus.text = $"Audio failed: {exception.Message}";
            }
        }

        private async void OnDetailClicked()
        {
            await Ctx.UI.OpenUI<DetailPage>(new DetailPage.Args());
        }

        private void OnBackClicked()
        {
            Ctx.UI.Back();
        }

        private void RefreshTexts()
        {
            Bindings.TxtTitle.text = Get("home.title");
            Bindings.TxtStatus.text = Get("home.status");
            Bindings.BtnLocale.GetComponentInChildren<Text>().text = Get("home.locale");
            Bindings.BtnSave.GetComponentInChildren<Text>().text = Get("home.save");
            Bindings.BtnLoad.GetComponentInChildren<Text>().text = Get("home.load");
            Bindings.BtnAudio.GetComponentInChildren<Text>().text = Get("home.audio");
            Bindings.BtnDetail.GetComponentInChildren<Text>().text = Get("home.detail");
            Bindings.BtnBack.GetComponentInChildren<Text>().text = Get("home.back");
        }

        private string Get(string key) => Ctx.Services.Get<ILocalizationService>().Get(key);
    }
}
