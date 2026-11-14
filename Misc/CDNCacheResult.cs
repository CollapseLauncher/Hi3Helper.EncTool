using System;
using System.IO;
using System.Net;
// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper.EncTool;

/// <summary>
/// A record contains the result of the CDN cache.
/// </summary>
public record CDNCacheResult
{
    /// <summary>
    /// Whether the request is cached or not.<br/>
    /// </summary>
    public bool IsCached { get; set; }

    /// <summary>
    /// The local path of the cache file. This will be null if the request is not cached.<br/>
    /// </summary>
    public string? LocalCachePath { get; set; }

    /// <summary>
    /// The tag/hash of the cache file. This will be null if the request is not cached using hash-based cache (like ETag or Content-MD5).
    /// </summary>
    public string? CacheETag { get; set; }

    /// <summary>
    /// The expired time of the cache in UTC. This will return default value if the request is not cached using time-based cache.<br/>
    /// </summary>
    public DateTimeOffset CacheExpireTimeUtc { get; set; }

    /// <summary>
    /// The stream of the response content. This will never be null.<br/>
    /// A <see cref="CopyToStream"/> will be used if the response is actually determined to be cached. Otherwise, a <see cref="BridgedNetworkStream"/> will be used.<br/>
    /// </summary>
    public required Stream Stream { get; set; }

    /// <summary>
    /// The status code of the response.
    /// </summary>
    public HttpStatusCode StatusCode { get; set; } = 0;

    /// <summary>
    /// Whether the <see cref="CDNCacheResult.StatusCode"/> is a success status code (2xx) or not.
    /// </summary>
    public bool IsSuccessStatusCode => (int)StatusCode is > 199 and < 300;
}
