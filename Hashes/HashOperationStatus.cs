using System.Security.Cryptography;
using System.Threading;

namespace Hi3Helper.EncTool.Hashes;

/// <summary>
/// The status of the hash utility operation.
/// </summary>
public enum HashOperationStatus
{
    /// <summary>
    /// The operation was successful.
    /// </summary>
    Success,

    /// <summary>
    /// Whether the hash algorithm is not supported on the current platform.<br/>
    /// For Windows scenario, these list of built-in SHA hash aren't supported on some platform:
    /// <list type="bullet">
    ///     <item>
    ///         <term><see cref="SHA3_256"/></term>
    ///         <description>Only supported on Windows 11</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="SHA3_384"/></term>
    ///         <description>Only supported on Windows 11</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="SHA3_512"/></term>
    ///         <description>Only supported on Windows 11</description>
    ///     </item>
    /// </list>
    /// </summary>
    HashNotSupported,

    /// <summary>
    /// The destination hash buffer is too small.
    /// </summary>
    DestinationBufferTooSmall,

    /// <summary>
    /// Whether the operation of the hash algorithm is invalid.<br/>
    /// For example, using <see cref="HMAC"/>-based hash without providing the secret-key.
    /// </summary>
    InvalidOperation,

    /// <summary>
    /// Whether the operation was cancelled by the given <see cref="CancellationToken"/> or the operation was timed-out.
    /// </summary>
    OperationCancelled
}