using TMPro;
using UnityEngine;

namespace HSE.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class UiTextBinding : MonoBehaviour
    {
        private TMP_Text _text;

        public void Apply(string value)
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text.text != value) _text.text = value;
        }
    }
}
