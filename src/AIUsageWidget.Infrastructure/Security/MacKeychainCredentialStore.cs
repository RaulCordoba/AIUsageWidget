using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using AIUsageWidget.Core.Interfaces;

namespace AIUsageWidget.Infrastructure.Security;

/// <summary>Secrets stay in the user's login Keychain, never in files or process arguments.</summary>
[SupportedOSPlatform("macos")]
public sealed class MacKeychainCredentialStore : ISecureCredentialStore
{
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private static readonly byte[] Service = Encoding.UTF8.GetBytes("AIUsageWidget");
    private const int NotFound = -25300;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = Encoding.UTF8.GetBytes(key);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service,
            (uint)account.Length, account, out var length, out var data, out var item);
        if (status == NotFound) return Task.FromResult<string?>(null);
        Check(status);
        try
        {
            return Task.FromResult<string?>(Marshal.PtrToStringUTF8(data, checked((int)length)));
        }
        finally
        {
            SecKeychainItemFreeContent(IntPtr.Zero, data);
            CFRelease(item);
        }
    }

    public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = Encoding.UTF8.GetBytes(key);
        var bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service,
                (uint)account.Length, account, out _, out var data, out var item);
            if (status == NotFound)
            {
                Check(SecKeychainAddGenericPassword(IntPtr.Zero, (uint)Service.Length, Service,
                    (uint)account.Length, account, (uint)bytes.Length, bytes, out item));
                CFRelease(item);
            }
            else
            {
                Check(status);
                try
                {
                    SecKeychainItemFreeContent(IntPtr.Zero, data);
                    Check(SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)bytes.Length, bytes));
                }
                finally { CFRelease(item); }
            }
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = Encoding.UTF8.GetBytes(key);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service,
            (uint)account.Length, account, out _, out var data, out var item);
        if (status == NotFound) return Task.CompletedTask;
        Check(status);
        try
        {
            SecKeychainItemFreeContent(IntPtr.Zero, data);
            Check(SecKeychainItemDelete(item));
        }
        finally { CFRelease(item); }
        return Task.CompletedTask;
    }

    private static void Check(int status)
    {
        if (status != 0) throw new InvalidOperationException($"No se pudo acceder al llavero de macOS (OSStatus {status}).");
    }

    [DllImport(Security)] private static extern int SecKeychainFindGenericPassword(IntPtr keychain,
        uint serviceLength, byte[] service, uint accountLength, byte[] account,
        out uint passwordLength, out IntPtr passwordData, out IntPtr item);
    [DllImport(Security)] private static extern int SecKeychainAddGenericPassword(IntPtr keychain,
        uint serviceLength, byte[] service, uint accountLength, byte[] account,
        uint passwordLength, byte[] passwordData, out IntPtr item);
    [DllImport(Security)] private static extern int SecKeychainItemModifyAttributesAndData(IntPtr item,
        IntPtr attributes, uint length, byte[] data);
    [DllImport(Security)] private static extern int SecKeychainItemDelete(IntPtr item);
    [DllImport(Security)] private static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr item);
}
