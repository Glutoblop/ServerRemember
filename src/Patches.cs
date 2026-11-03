using HarmonyLib;

namespace ServerRemember
{
    internal static class Session
    {
        /// <summary>Key of the server we're currently joining (type + address/id).</summary>
        internal static string ServerKey;

        /// <summary>Password typed into the dialog this connection attempt; saved only once accepted.</summary>
        internal static string PendingPassword;

        internal static string KeyFor(ServerJoinData data)
        {
            if (!data.IsValid) return null;
            return data.m_type + ":" + data.ToString();
        }
    }

    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.JoinServer))]
    internal static class JoinServerPatch
    {
        private static void Prefix(FejdStartup __instance)
        {
            Session.ServerKey = Session.KeyFor(__instance.m_joinServer);
            Session.PendingPassword = null;
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_ClientHandshake))]
    internal static class ClientHandshakePatch
    {
        private static void Postfix(ZNet __instance, bool needPassword)
        {
            if (!Plugin.Enabled.Value || !needPassword) return;
            if (__instance.m_passwordDialog == null || !__instance.m_passwordDialog.gameObject.activeSelf) return;

            bool hasSaved = Session.ServerKey != null && PasswordStore.Has(Session.ServerKey);
            PasswordButton.Show(__instance, hasSaved);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnPasswordEntered))]
    internal static class PasswordEnteredPatch
    {
        private static void Prefix(ZNet __instance, string pwd)
        {
            // Mirror vanilla's guard so we only remember passwords that were actually sent.
            if (__instance.m_tempPasswordRPC == null || !__instance.m_tempPasswordRPC.IsConnected()) return;
            if (string.IsNullOrEmpty(pwd)) return;
            Session.PendingPassword = pwd;
            PasswordButton.Hide();
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
    internal static class PeerInfoPatch
    {
        private static void Postfix(ZNet __instance)
        {
            if (ZNet.m_isServer ||Session.PendingPassword == null) return;
            if (ZNet.m_connectionStatus != ZNet.ConnectionStatus.Connected) return;

            if (Plugin.Enabled.Value && Session.ServerKey != null)
            {
                PasswordStore.Set(Session.ServerKey, Session.PendingPassword);
                Plugin.Log("Saved password for " + Session.ServerKey);
            }
            Session.PendingPassword = null;
        }
    }
}
