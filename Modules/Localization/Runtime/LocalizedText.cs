using Cascade.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Modules.Localization
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;
        [SerializeField] private Text _text;
        [SerializeField] private TMP_Text _tmp;

        private ILocalizationService _subscribed;

        public string Key
        {
            get => _key;
            set
            {
                _key = value;
                if (isActiveAndEnabled)
                    Apply();
            }
        }

        private void Reset()
        {
            EnsureTargets();
        }

        private void OnEnable()
        {
            EnsureTargets();
            SubscribeCurrent();
            Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            EnsureTargets();
            if (isActiveAndEnabled)
                Apply();
        }

        public void Apply()
        {
            EnsureTargets();
            var value = LocalizationAccess.Get(_key);
            if (_text != null)
                _text.text = value;
            if (_tmp != null)
            {
                _tmp.text = value;
                _tmp.havePropertiesChanged = true;
            }
        }

        public void RebindAccess()
        {
            Unsubscribe();
            if (isActiveAndEnabled)
                SubscribeCurrent();
            Apply();
        }

        private void EnsureTargets()
        {
            if (_text == null)
                _text = GetComponent<Text>();
            if (_tmp == null)
                _tmp = GetComponent<TMP_Text>();
        }

        private void SubscribeCurrent()
        {
            if (!LocalizationAccess.TryGet(out var localization))
                return;
            if (ReferenceEquals(_subscribed, localization))
                return;
            Unsubscribe();
            _subscribed = localization;
            localization.LocaleChanged += OnLocaleChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribed == null)
                return;
            _subscribed.LocaleChanged -= OnLocaleChanged;
            _subscribed = null;
        }

        private void OnLocaleChanged(string _)
        {
            Apply();
        }
    }
}
