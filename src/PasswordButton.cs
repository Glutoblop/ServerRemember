using System.Linq;
using GUIFramework;
using UnityEngine;
using UnityEngine.UI;

namespace ServerRemember
{
    // A small square key button pinned to the right of the GuiInputField in ZNet.m_passwordDialog.
    // Always shown while the prompt is open; disabled when there's no saved password to use.
    // Uses the game's own sprites: "key_icon" (gold key) on "key_base" (square button frame). Both live in
    // the same GUI bundle as the password panel art ("woodpanel_password"), so they're loaded at this point.
    internal static class PasswordButton
    {
        private const string IconSprite = "key_icon";
        private const string FrameSprite = "key_base";

        private static Button _button;
        private static Image _icon;
        private static GuiInputField _input;

        internal static void Show(ZNet znet, bool hasSaved)
        {
            _input = znet.m_passwordDialog.GetComponentInChildren<GuiInputField>(true);
            if (_input == null) return;

            if (_button == null || _button.transform.parent != _input.transform)
                Create();

            Layout();
            _button.interactable = hasSaved;
            _icon.color = hasSaved ? Color.white : new Color(1f, 1f, 1f, 0.3f);
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
            _icon = null;
        }

        private static Sprite FindSprite(string name)
        {
            var loaded = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == name);
            if (loaded != null) return loaded;

            // Not instantiated yet: pull it straight from whichever loaded bundle has it.
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null || bundle.isStreamedSceneAssetBundle) continue;
                foreach (var path in bundle.GetAllAssetNames())
                {
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;
                    var sprite = bundle.LoadAsset<Sprite>(path);
                    if (sprite != null) return sprite;
                }
            }

            Plugin.Warn("Sprite '" + name + "' not found");
            return null;
        }

        private static void Create()
        {
            Destroy();

            var go = new GameObject("ServerRemember_KeyButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_input.transform, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            var frame = go.GetComponent<Image>();
            frame.sprite = FindSprite(FrameSprite);
            frame.type = Image.Type.Sliced;
            if (frame.sprite == null) frame.color = new Color(0.15f, 0.12f, 0.1f, 0.9f);

            _button = go.GetComponent<Button>();
            _button.targetGraphic = frame;
            var colors = _button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
            colors.pressedColor = new Color(0.8f, 0.65f, 0.4f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            _button.colors = colors;
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(OnClick);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(5f, 5f);
            iconRt.offsetMax = new Vector2(-5f, -5f);
            _icon = iconGo.GetComponent<Image>();
            _icon.sprite = FindSprite(IconSprite);
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
        }

        private static void Layout()
        {
            var inputRt = (RectTransform)_input.transform;
            var rt = (RectTransform)_button.transform;
            float size = Mathf.Clamp(inputRt.rect.height, 24f, 48f);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(Plugin.ButtonGap.Value, 0f);
            rt.SetAsLastSibling();
        }

        private static void OnClick()
        {
            string key = Session.LookupKey();
            if (_input == null || key == null) return;
            string pwd = PasswordStore.Get(key);
            if (string.IsNullOrEmpty(pwd)) return;

            _input.text = pwd;
            _input.ActivateInputField();
            _input.caretPosition = pwd.Length;
        }
    }
}
