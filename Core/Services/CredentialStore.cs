using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Launcher.Core.Services;

/// <summary>
/// Guarda usuario y contrasena en el Administrador de credenciales de Windows (cifrado con tu cuenta de
/// Windows). Nunca se escriben en commands.json: alli solo queda el nombre de la credencial.
/// </summary>
public static class CredentialStore
{
    private const uint CredTypeGeneric = 1;
    private const uint PersistLocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    public static bool Save(string key, string user, string password)
    {
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var credential = new CREDENTIAL
            {
                Type = CredTypeGeneric,
                TargetName = key,
                UserName = user,
                CredentialBlob = blob,
                CredentialBlobSize = (uint)(password.Length * 2),
                Persist = PersistLocalMachine
            };

            return CredWrite(ref credential, 0);
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo guardar la credencial", ex);
            return false;
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    }

    /// <summary>Devuelve (usuario, contrasena) o null si no existe.</summary>
    public static (string User, string Password)? Read(string key)
    {
        try
        {
            if (!CredRead(key, CredTypeGeneric, 0, out var pointer)) return null;

            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIAL>(pointer);
                var password = credential.CredentialBlob == IntPtr.Zero
                    ? ""
                    : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2) ?? "";
                return (credential.UserName ?? "", password);
            }
            finally
            {
                CredFree(pointer);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo leer la credencial", ex);
            return null;
        }
    }

    public static bool Exists(string key) => Read(key) != null;

    public static void Delete(string key)
    {
        try { CredDelete(key, CredTypeGeneric, 0); }
        catch { /* si no existia, no pasa nada */ }
    }
}
