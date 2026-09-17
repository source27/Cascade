using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 可选：把 Application.version（及产品名）写到 UGUI Text；
    /// 若存在 TMP_Text 组件则反射赋值（无硬性 TMP 程序集依赖）。
    /// </summary>
    public sealed class VersionLabelBinder : MonoBehaviour
    {
        [SerializeField] Text _uguiText;
        [SerializeField] Component _tmpText;
        [SerializeField] bool _includeProductName = true;
        [SerializeField] string _prefix = "v";

        void OnEnable()
        {
            if (_uguiText == null)
                _uguiText = GetComponent<Text>();
            Refresh();
        }

        public void Refresh()
        {
            var ver = Application.version;
            if (string.IsNullOrEmpty(ver))
                ver = "0.0.0";
            var text = _includeProductName
                ? $"{Application.productName} {_prefix}{ver}"
                : $"{_prefix}{ver}";

            if (_uguiText != null)
                _uguiText.text = text;

            var tmp = _tmpText != null ? _tmpText : GetComponent("TMP_Text");
            if (tmp != null)
            {
                var prop = tmp.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                prop?.SetValue(tmp, text);
            }
        }
    }
}
