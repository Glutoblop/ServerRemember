using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ServerRemember
{
    // "Reconnect: <server> [icon] <character>" at the top of the main menu (FejdStartup.m_menuList).
    //   Server:    first entry of the vanilla Recent list (recent servers file, ServerListGui.GetServerListLocations),
    //              which FejdStartup.JoinServer updates on every join. Its display name is remembered by us when
    //              joining, since the list itself only stores addresses/ids.
    //   Character: FejdStartup.m_profiles[m_profileIndex], the one the character screen has selected (set from the
    //              "profile" pref at startup). Its icon is a head-and-shoulders render of FejdStartup.m_playerInstance,
    //              the preview character vanilla builds for that profile (SetupCharacterPreview).
    // Clicking does what the character screen's Start + the server list's Join do: SelectCharacter, then
    // SetServerToJoin + JoinServer. The password prompt (with the saved-password key button) follows as usual.
    internal class MainMenuReconnect : MonoBehaviour
    {
        private const string ObjectName = "ServerRemember_Reconnect";
        private const string NamePrefsPrefix = "ServerRemember.ServerName.";
        private const int IconPixels = 128;
        private const float IconRenderInterval = 0.25f;

        private FejdStartup _startup;
        private Button _button;
        private TMP_Text _label;
        private TMP_Text _charName;
        private RawImage _icon;
        private RenderTexture _iconTexture;
        private Camera _iconCamera;
        private float _nextIconRender;
        private ServerJoinData _server;
        private string _serverName;
        private LocalServerList _recent;
        private float _nextNameQuery;
        private const float NameQueryInterval = 1f;

        // ---- remembering server names --------------------------------------------------------------------

        internal static void RememberName(ServerJoinData data)
        {
            if (data.IsValid) KnownName(data);
        }

        // The server's display name: the game's own name cache (filled from the server list files and from
        // matchmaking replies), else the last name we saw for it. Null until one is known; never the address.
        private static string KnownName(ServerJoinData data)
        {
            string key = NamePrefsPrefix + Session.KeyFor(data);
            string name = null;
            try
            {
                if (MultiBackendMatchmaking.TryGetServerName(data, out string n)) name = n;
                if (string.IsNullOrEmpty(name)) name = MultiBackendMatchmaking.GetServerMatchmakingData(data).m_serverName;
            }
            catch { }

            if (!string.IsNullOrEmpty(name) && name != data.ToString())
            {
                if (PlayerPrefs.GetString(key, "") != name)
                {
                    PlayerPrefs.SetString(key, name);
                    PlayerPrefs.Save();
                }
                return name;
            }
            name = PlayerPrefs.GetString(key, "");
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private static ServerJoinData LastServer()
        {
            var recent = new LocalServerList(null, ServerListGui.GetServerListLocations("recent"));
            try { return recent.Count > 0 ? recent[0] : ServerJoinData.None; }
            finally { recent.Dispose(); }
        }

        // ---- building --------------------------------------------------------------------------------------

        internal static void Attach(FejdStartup startup)
        {
            if (!Plugin.ReconnectEnabled.Value || startup.m_menuList == null) return;
            if (startup.m_menuList.transform.Find(ObjectName) != null) return;

            Button template = startup.m_menuList.GetComponentInChildren<Button>(true);
            if (template == null) { Plugin.Warn("Main menu has no buttons to copy"); return; }

            GameObject go = Instantiate(template.gameObject, template.transform.parent, false);
            go.name = ObjectName;
            go.transform.SetSiblingIndex(template.transform.GetSiblingIndex()); // top of the menu
            StripLocalisation(go);

            var rb = go.AddComponent<MainMenuReconnect>();
            rb._startup = startup;
            rb._button = go.GetComponent<Button>();
            rb._button.onClick = new Button.ButtonClickedEvent(); // drop the template's persistent listener
            rb._button.onClick.AddListener(rb.OnClick);
            rb._label = go.GetComponentInChildren<TMP_Text>(true);
            if (rb._label == null) { Destroy(go); Plugin.Warn("Main menu button has no text"); return; }
            rb.BuildExtras();
            rb.Refresh();
        }

        private static void StripLocalisation(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (c is UIGamePad || c.GetType().Name == "Localize") DestroyImmediate(c);
        }

        private void BuildExtras()
        {
            // Character name: a copy of the label so it matches font, size and colour.
            GameObject nameGo = Instantiate(_label.gameObject, _label.transform, false);
            nameGo.name = "Character";
            foreach (Transform child in nameGo.transform) Destroy(child.gameObject);
            StripLocalisation(nameGo);
            _charName = nameGo.GetComponent<TMP_Text>();
            _charName.enableAutoSizing = false;
            _charName.textWrappingMode = TextWrappingModes.NoWrap;
            _charName.alignment = TextAlignmentOptions.Left;
            _charName.raycastTarget = false;
            var nrt = (RectTransform)nameGo.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrt.pivot = new Vector2(0f, 0.5f);

            var iconGo = new GameObject("CharacterIcon", typeof(RectTransform), typeof(RawImage));
            iconGo.layer = _label.gameObject.layer;
            var irt = (RectTransform)iconGo.transform;
            irt.SetParent(_label.transform, false);
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            _icon = iconGo.GetComponent<RawImage>();
            _icon.raycastTarget = false;

            _iconTexture = new RenderTexture(IconPixels, IconPixels, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = ObjectName };
            _icon.texture = _iconTexture;

            var camGo = new GameObject(ObjectName + "_Camera");
            camGo.transform.SetParent(transform, false);
            _iconCamera = camGo.AddComponent<Camera>();
            _iconCamera.enabled = false; // rendered by hand
            _iconCamera.clearFlags = CameraClearFlags.SolidColor;
            _iconCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _iconCamera.fieldOfView = 30f;
            _iconCamera.nearClipPlane = 0.05f;
            _iconCamera.farClipPlane = 3f;
            _iconCamera.allowHDR = false;
            _iconCamera.allowMSAA = true;
            _iconCamera.targetTexture = _iconTexture;
        }

        private void OnDestroy()
        {
            CloseRecent();
            if (_iconTexture != null) { _iconTexture.Release(); Destroy(_iconTexture); }
        }

        private void OnDisable() => CloseRecent();

        private void CloseRecent()
        {
            _recent?.Dispose();
            _recent = null;
        }

        // ---- per-frame -------------------------------------------------------------------------------------

        private void OnEnable()
        {
            if (_label != null) Refresh(); // the main menu was shown again
        }

        private PlayerProfile SelectedProfile()
        {
            var profiles = _startup != null ? _startup.m_profiles : null;
            int i = _startup != null ? _startup.m_profileIndex : -1;
            return profiles != null && i >= 0 && i < profiles.Count ? profiles[i] : null;
        }

        // Keep the vanilla Recent list open while the menu shows: loading it fills the game's name cache from the
        // file, and ticking it asks the server for its current name (as the server browser does).
        private void Refresh()
        {
            CloseRecent();
            _recent = new LocalServerList(null, ServerListGui.GetServerListLocations("recent"));
            _server = _recent.Count > 0 ? _recent[0] : ServerJoinData.None;
            _serverName = _server.IsValid ? KnownName(_server) : null;
            _nextNameQuery = 0f;
            _nextIconRender = 0f;
        }

        private void UpdateServerName()
        {
            if (!_server.IsValid || _recent == null || Time.unscaledTime < _nextNameQuery) return;
            _nextNameQuery = Time.unscaledTime + NameQueryInterval;
            try { _recent.Tick(); } catch (System.Exception e) { Plugin.Warn("Server name lookup failed: " + e.Message); }
            string name = KnownName(_server);
            if (name != null) _serverName = name;
        }

        private void LateUpdate()
        {
            if (!Plugin.ReconnectEnabled.Value) { gameObject.SetActive(false); return; }

            PlayerProfile profile = SelectedProfile();
            bool usable = _server.IsValid && profile != null;
            _button.interactable = usable;

            UpdateServerName();
            _label.text = !_server.IsValid ? "Reconnect"
                        : "Reconnect: " + (_serverName ?? "last server");
            _charName.text = profile != null ? profile.GetName() : "";
            Layout(profile != null);

            // Follow the label's hover/press tint.
            _charName.canvasRenderer.SetColor(_label.canvasRenderer.GetColor());

            if (profile != null && Time.unscaledTime >= _nextIconRender)
            {
                _nextIconRender = Time.unscaledTime + IconRenderInterval;
                RenderIcon();
            }
        }

        // Place "[icon] Name" straight after the end of the label's text, at the text's height.
        private void Layout(bool showCharacter)
        {
            _icon.gameObject.SetActive(showCharacter);
            _charName.gameObject.SetActive(showCharacter);
            if (!showCharacter) return;

            _label.ForceMeshUpdate();
            Bounds b = _label.textBounds;
            float size = _label.fontSize * 1.15f;
            float gap = Plugin.ReconnectGap.Value;
            var labelRt = (RectTransform)_label.transform;
            // textBounds are local to the label; our children are anchored to its centre, so convert.
            Vector2 centreOffset = (Vector2.one * 0.5f - labelRt.pivot) * labelRt.rect.size;
            float x = b.max.x - centreOffset.x + gap;
            float y = b.center.y - centreOffset.y;

            var irt = (RectTransform)_icon.transform;
            irt.sizeDelta = new Vector2(size, size);
            irt.anchoredPosition = new Vector2(x, y);

            _charName.fontSize = _label.fontSize;
            var nrt = (RectTransform)_charName.transform;
            nrt.sizeDelta = new Vector2(_charName.preferredWidth + 4f, size);
            nrt.anchoredPosition = new Vector2(x + size + gap * 0.5f, y);
        }

        private void RenderIcon()
        {
            GameObject player = _startup.m_playerInstance;
            if (player == null) { _icon.color = Color.clear; return; }

            Transform head = null;
            var anim = player.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
            Vector3 focus = head != null ? head.position + Vector3.up * 0.05f : player.transform.position + Vector3.up * 1.65f;

            int mask = 0;
            foreach (var r in player.GetComponentsInChildren<Renderer>()) mask |= 1 << r.gameObject.layer;
            if (mask == 0) return;

            _iconCamera.cullingMask = mask;
            _iconCamera.transform.position = focus + player.transform.forward * 1.0f - Vector3.up * 0.05f;
            _iconCamera.transform.LookAt(focus);
            _iconCamera.Render();
            _icon.color = Color.white;
        }

        private void OnClick()
        {
            PlayerProfile profile = SelectedProfile();
            ServerJoinData server = LastServer();
            if (profile == null || !server.IsValid) return;

            Plugin.Log($"Reconnecting to {server} as {profile.GetName()}");
            _startup.SelectCharacter(profile.GetFilename(), profile.m_fileSource);
            _startup.SetServerToJoin(server);
            _startup.JoinServer();
        }
    }

    [HarmonyPatch(typeof(FejdStartup), "Start")]
    internal static class FejdStartup_Start
    {
        private static void Postfix(FejdStartup __instance) => MainMenuReconnect.Attach(__instance);
    }
}
