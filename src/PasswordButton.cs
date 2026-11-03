using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ServerRemember
{
    // Adds a "Use saved password" button under the GuiInputField in ZNet.m_passwordDialog.
    // We clone an existing game button so it matches the menu style, then strip its localization
    // and click handlers.
    internal static class PasswordButton
    {
        private static Button _button;
        private static TMP_Text _label;
        private static GuiInputField _input;

        internal static void Show(ZNet znet, bool hasSaved)
        {
            if (!hasSaved)
            {
                Hide();
                return;
            }

            _input = znet.m_passwordDialog.GetComponentInChildren<GuiInputField>(true);
            if (_input == null) return;

            if (_button == null || _button.transform.parent != _input.transform.parent)
                Create(znet.m_passwordDialog);
            if (_button == null) return;

            Layout();
            if (_label != null) _label.text = Plugin.ButtonText.Value;
            _button.gameObject.SetActive(true);
        }

        internal static void Hide()
        {
            if (_button != null) _button.gameObject.SetActive(false);
        }

        internal static void Destroy()
        {
            if (_button != null) Object.Destroy(_button.gameObject);
            _button = null;
            _label = null;
        }

        private static Button FindTemplate(RectTransform dialog)
        {
            foreach (var b in dialog.GetComponentsInChildren<Button>(true))
                if (b.GetComponentInChildren<TMP_Text>(true) != null) return b;

            var fejd = FejdStartup.instance;
            if (fejd != null)
                foreach (var b in fejd.GetComponentsInChildren<Button>(true))
                    if (b.GetComponentInChildren<TMP_Text>(true) != null) return b;

            return null;
        }

        private static void Create(RectTransform dialog)
        {
            Destroy();

            var template = FindTemplate(dialog);
            GameObject go;
            if (template != null)
            {
                go = Object.Instantiate(template.gameObject, _input.transform.parent, false);
                // Strip anything that would rewrite the text or react to gamepad hints.
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    string n = mb.GetType().Name;
                    if (n == "Localize" || n == "UIGamePad") Object.DestroyImmediate(mb);
                }
                _button = go.GetComponent<Button>();
                _button.onClick = new Button.ButtonClickedEvent();
                _label = go.GetComponentInChildren<TMP_Text>(true);
            }
            else
            {
                go = BuildPlain();
            }

            go.name = "ServerRemember_UseSavedPassword";
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            _button.onClick.AddListener(OnClick);
        }

        private static GameObject BuildPlain()
        {
            var go = new GameObject("ServerRemember_UseSavedPassword", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_input.transform.parent, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.15f, 0.9f);
            _button = go.GetComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (_input.textComponent != null) tmp.font = _input.textComponent.font;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.6f);
            _label = tmp;
            return go;
        }

        private static void Layout()
        {
            var inputRt = (RectTransform)_input.transform;
            var rt = (RectTransform)_button.transform;
            rt.anchorMin = inputRt.anchorMin;
            rt.anchorMax = inputRt.anchorMax;
            rt.pivot = inputRt.pivot;

            float height = Mathf.Max(30f, inputRt.rect.height);
            // Same width and horizontal placement as the input field, directly below it.
            rt.sizeDelta = new Vector2(inputRt.sizeDelta.x, height);
            rt.anchoredPosition = inputRt.anchoredPosition +
                                  new Vector2(0f, -(inputRt.rect.height * inputRt.pivot.y + Plugin.ButtonOffsetY.Value + height * (1f - inputRt.pivot.y)));
            rt.SetAsLastSibling();
        }

        private static void OnClick()
        {
            if (_input == null || Session.ServerKey == null) return;
            string pwd = PasswordStore.Get(Session.ServerKey);
            if (string.IsNullOrEmpty(pwd))
            {
                Hide();
                return;
            }

            _input.text = pwd;
            if (Plugin.AutoSubmit.Value)
            {
                _input.OnInputSubmit?.Invoke(pwd);
            }
            else
            {
                _input.ActivateInputField();
                _input.caretPosition = pwd.Length;
            }
        }
    }
}
