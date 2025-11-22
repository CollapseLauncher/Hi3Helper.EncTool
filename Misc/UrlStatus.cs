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
    private const int URLStrLen = 512 - 16;

    /// <summary>
    /// The status code of the response.
    /// </summary>
    public readonly HttpStatusCode StatusCode;

    /// <summary>
    /// The size of the response data.
    /// </summary>
    public readonly long FileSize;

    private fixed byte _urlString[URLStrLen]; // Fit to 512 bytes

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
                ReadOnlySpan<byte> buffer = MemoryMarshal
                   .CreateReadOnlySpanFromNullTerminated(bufferP);
                return Encoding.UTF8.GetString(buffer);
            }
        }
    }

    internal UrlStatus(HttpResponseMessage message)
        : this(message.StatusCode, message.RequestMessage?.RequestUri?.AbsoluteUri ?? "")
    {
        FileSize = message.Content.Headers.ContentLength ?? 0;
    }

    internal UrlStatus(HttpStatusCode statusCode, string url)
    {
        StatusCode = statusCode;

        fixed (byte* urlBytesP = _urlString)
        {
            Span<byte> urlBytesSpan = new Span<byte>(urlBytesP, URLStrLen);
            urlBytesSpan.Clear();
            Encoding.UTF8.GetBytes(url, urlBytesSpan[..(URLStrLen - 1)]);
        }
    }
}
