using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable CheckNamespace
namespace Hi3Helper.EncTool;

/// <summary>
/// A record contains the status of the response from a URL.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct UrlStatus
{
    private const int URLStrLen = 1024 - (16 + 40);

    /// <summary>
    /// The status code of the response.
    /// </summary>
    public readonly HttpStatusCode StatusCode;

    /// <summary>
    /// The size of the response data.
    /// </summary>
    public readonly long FileSize;

    private fixed byte _urlString[URLStrLen]; // Fit to 968 bytes

    /// <summary>
    /// Version of the struct. If the value is 0 or 1, then assume it's V1. Otherwise, higher.
    /// </summary>
    private int Version;

    private ulong _reserved1;
    private ulong _reserved2;
    private ulong _reserved3;
    private ulong _reserved4;

    /// <summary>
    /// Whether the <see cref="UrlStatus.StatusCode"/> is a success status code (2xx) or not.
    /// </summary>
    public bool IsSuccessStatusCode => (int)StatusCode is > 199 and < 300;

    /// <summary>
    /// Throws if the HTTP Status returns unsuccessful code.
    /// </summary>
    /// <exception cref="HttpRequestException"/>
    public void EnsureSuccessStatusCode()
    {
        if (!IsSuccessStatusCode)
        {
            throw new
                HttpRequestException($"URL: {Url} returns unsuccessful return code: {(int)StatusCode} ({StatusCode})");
        }
    }

    /// <summary>
    /// Gets the URL string of the request
    /// </summary>
    public string Url
    {
        get
        {
            fixed (byte* bufferP = _urlString)
            {
                if (bufferP == null)
                {
                    return "";
                }

                ReadOnlySpan<byte> buffer = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(bufferP);
                return buffer.IsEmpty ? "" : Encoding.UTF8.GetString(buffer);
            }
        }
    }

    internal UrlStatus(HttpResponseMessage message, Uri? originUrl)
        : this(message.StatusCode, originUrl, message.RequestMessage?.RequestUri)
    {
        FileSize = message.Content.Headers.ContentLength ?? 0;
    }

    internal UrlStatus(HttpStatusCode statusCode, Uri? originUrl, Uri? responseUrl)
    {
        StatusCode = statusCode;
        Version    = 1;

        if (responseUrl != null &&
            originUrl != null &&
            originUrl != responseUrl)
        {
            responseUrl = originUrl;
        }

        responseUrl ??= originUrl;
        string url = responseUrl?.AbsoluteUri ?? "";

        if (!string.IsNullOrEmpty(url))
        {
            fixed (byte* urlBytesP = _urlString)
            {
                Span<byte> urlBytesSpan = new(urlBytesP, URLStrLen);
                urlBytesSpan.Clear();

                Encoding.UTF8.TryGetBytes(url, urlBytesSpan[..(URLStrLen - 1)], out _);
            }
        }
    }
}
