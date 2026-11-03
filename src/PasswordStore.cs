using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx;

namespace ServerRemember
{
    // A hash is one-way, so it can't be "unhashed" back into a password. Instead the password is
    // encrypted. On Windows we use DPAPI (CryptProtectData, current-user scope): only your Windows
    // account on this PC can decrypt the file. Elsewhere we fall back to AES with a key derived from
    // the user/machine name, which hides the password from casual reading but is not strong protection.
    internal static class PasswordStore
    {
        private const string WinPrefix = "dpapi:";
        private const string AesPrefix = "aes:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("jordan.valheim.serverremember");

        private static readonly Dictionary<string, string> Entries = new Dictionary<string, string>();

        private static string FilePath => Path.Combine(Paths.ConfigPath, Plugin.Guid + ".passwords");

        internal static void Load()
        {
            Entries.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    Entries[line.Substring(0, tab)] = line.Substring(tab + 1);
                }
            }
            catch (Exception e)
            {
                Plugin.Warn("Could not read saved passwords: " + e.Message);
            }
        }

        private static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in Entries) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch (Exception e)
            {
                Plugin.Warn("Could not write saved passwords: " + e.Message);
            }
        }

        internal static bool Has(string key) => Entries.ContainsKey(key);

        internal static void Set(string key, string password)
        {
            string enc = Encrypt(password);
            if (enc == null) return;
            Entries[key] = enc;
            Save();
        }

        internal static string Get(string key)
        {
            if (!Entries.TryGetValue(key, out var enc)) return null;
            try { return Decrypt(enc); }
            catch (Exception e)
            {
                Plugin.Warn("Could not decrypt saved password for " + key + ": " + e.Message);
                return null;
            }
        }

        private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        private static string Encrypt(string plain)
        {
            byte[] data = Encoding.UTF8.GetBytes(plain);
            try
            {
                if (IsWindows) return WinPrefix + Convert.ToBase64String(Dpapi.Protect(data, Entropy));
            }
            catch (Exception e)
            {
                Plugin.Warn("DPAPI unavailable, using fallback encryption: " + e.Message);
            }
            return AesPrefix + Convert.ToBase64String(AesCrypt(data, encrypt: true));
        }

        private static string Decrypt(string stored)
        {
            if (stored.StartsWith(WinPrefix))
                return Encoding.UTF8.GetString(Dpapi.Unprotect(Convert.FromBase64String(stored.Substring(WinPrefix.Length)), Entropy));
            if (stored.StartsWith(AesPrefix))
                return Encoding.UTF8.GetString(AesCrypt(Convert.FromBase64String(stored.Substring(AesPrefix.Length)), encrypt: false));
            return null;
        }

        // Fallback: AES-CBC, random IV prepended, key = SHA256(user|machine|entropy).
        private static byte[] AesCrypt(byte[] input, bool encrypt)
        {
            byte[] key;
            using (var sha = SHA256.Create())
                key = sha.ComputeHash(Encoding.UTF8.GetBytes(Environment.UserName + "|" + Environment.MachineName + "|" + Plugin.Guid));

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                if (encrypt)
                {
                    aes.GenerateIV();
                    using (var enc = aes.CreateEncryptor())
                    {
                        byte[] body = enc.TransformFinalBlock(input, 0, input.Length);
                        byte[] result = new byte[aes.IV.Length + body.Length];
                        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                        Buffer.BlockCopy(body, 0, result, aes.IV.Length, body.Length);
                        return result;
                    }
                }

                byte[] iv = new byte[16];
                Buffer.BlockCopy(input, 0, iv, 0, 16);
                aes.IV = iv;
                using (var dec = aes.CreateDecryptor())
                    return dec.TransformFinalBlock(input, 16, input.Length - 16);
            }
        }

        private static class Dpapi
        {
            [StructLayout(LayoutKind.Sequential)]
            private struct DataBlob
            {
                public int cbData;
                public IntPtr pbData;
            }

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool CryptProtectData(ref DataBlob dataIn, string description, ref DataBlob entropy,
                IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy,
                IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

            [DllImport("kernel32.dll")]
            private static extern IntPtr LocalFree(IntPtr mem);

            private const int UiForbidden = 0x1;

            internal static byte[] Protect(byte[] data, byte[] entropy) => Run(data, entropy, protect: true);
            internal static byte[] Unprotect(byte[] data, byte[] entropy) => Run(data, entropy, protect: false);

            private static byte[] Run(byte[] data, byte[] entropy, bool protect)
            {
                var hData = GCHandle.Alloc(data, GCHandleType.Pinned);
                var hEnt = GCHandle.Alloc(entropy, GCHandleType.Pinned);
                var outBlob = default(DataBlob);
                try
                {
                    var inBlob = new DataBlob { cbData = data.Length, pbData = hData.AddrOfPinnedObject() };
                    var entBlob = new DataBlob { cbData = entropy.Length, pbData = hEnt.AddrOfPinnedObject() };
                    bool ok = protect
                        ? CryptProtectData(ref inBlob, "ServerRemember", ref entBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, out outBlob)
                        : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, out outBlob);
                    if (!ok) throw new CryptographicException("DPAPI error " + Marshal.GetLastWin32Error());

                    var result = new byte[outBlob.cbData];
                    Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                    return result;
                }
                finally
                {
                    hData.Free();
                    hEnt.Free();
                    if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
                }
            }
        }
    }
}
