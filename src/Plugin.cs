using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ServerRemember
{
    // Client-only. Vanilla flow when joining a password-protected server:
    //   FejdStartup.JoinServer()           -> m_joinServer identifies the server we're joining
    //   ZNet.RPC_ClientHandshake(needPw)   -> shows ZNet.m_passwordDialog with a GuiInputField
    //   ZNet.OnPasswordEntered(pwd)        -> sends the salted hash to the server
    //   ZNet.RPC_PeerInfo (client side)    -> m_connectionStatus = Connected (password accepted)
    // We remember the typed password, and only store it (encrypted, per server) once RPC_PeerInfo
    // marks the connection as Connected. Nothing is sent to the server beyond what vanilla sends.
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jordan.valheim.serverremember";
        public const string Name = "Server Remember";
        public const string Version = "0.1.5";

        internal static Plugin Instance;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> ButtonGap;
        internal static ConfigEntry<bool> ReconnectEnabled;
        internal static ConfigEntry<float> ReconnectGap;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;

            Enabled = Config.Bind("General", "Enabled", true,
                "Remember the last password that successfully got you into each server, and show a button to re-enter it.");
            ButtonGap = Config.Bind("Layout", "ButtonGap", 6f,
                "Gap in pixels between the right edge of the password box and the key button.");
            ReconnectEnabled = Config.Bind("Reconnect", "Enabled", true,
                "Show a Reconnect button at the top of the main menu that joins the last server you played on with the selected character.");
            ReconnectGap = Config.Bind("Reconnect", "Gap", 8f,
                "Gap in pixels between the Reconnect text, the character icon and the character name.");

            PasswordStore.Load();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            PasswordButton.Destroy();
        }

        internal static void Log(string msg) => Instance?.Logger.LogInfo(msg);
        internal static void Warn(string msg) => Instance?.Logger.LogWarning(msg);
    }
}
