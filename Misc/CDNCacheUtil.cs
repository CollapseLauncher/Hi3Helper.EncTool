using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper.EncTool;

public static class CDNCacheUtil
{
    public static CDNCache Shared { get; set; } = new();

    /// <summary>
    /// Gets/sets the current directory for storing the cache locally.<br/>
    /// While the cache directory is set, this will also perform a cache garbage collection based on <see cref="MaxAcceptedCacheExpireTime"/>.<br/>
    /// <br/>
    /// To skip the cache garbage collection, use <see cref="SetCacheDirSkipGC"/>.
    /// </summary>
    public static string? CurrentCacheDir
    {
        get => Shared.CurrentCacheDir;
        set => Shared.CurrentCacheDir = value;
    }

    /// <summary>
    /// Gets/sets the logger for this cache utility.
    /// </summary>
    public static ILogger? Logger
    {
        get => Shared.Logger;
        set => Shared.Logger = value;
    }

    /// <summary>
    /// Whether the CDN cache is enabled or not.
    /// </summary>
    public static bool IsEnabled
    {
        get => Shared.IsEnabled;
        set => Shared.IsEnabled = value;
    }

    /// <summary>
    /// Whether the aggressive mode is enabled or not (Default: disabled).<br/>
    /// This mode will try to cache all requests, even if the ETag or expire header is not present in the response.<br/>
    /// <br/>
    /// This method is valid only if <see cref="IsEnabled"/> is set to true and if <see cref="TryGetCachedStreamFrom(HttpClient,string,HttpMethod,System.Threading.CancellationToken)"/> method is used.<br/>
    /// </summary>
    public static bool IsUseAggressiveMode
    {
        get => Shared.IsUseAggressiveMode;
        set => Shared.IsUseAggressiveMode = value;
    }

    /// <summary>
    /// Determine how long the maximum duration of the cache expire time is accepted (Default: 10 minutes).<br/>
    /// This will clamp the maximum duration of the cache expire time, even if the CDN provides a longer duration.<br/>
    /// <br/>
    /// This also sets how long the cache will be kept in the local directory before it is cleaned up, either it's hash-based or time-based cache.<br/>
    /// </summary>
    public static TimeSpan MaxAcceptedCacheExpireTime
    {
        get => Shared.MaxAcceptedCacheExpireTime;
        set => Shared.MaxAcceptedCacheExpireTime = value;
    }

    /// <summary>
    /// Set the cache directory to use for the CDN cache and skip garbage collection.<br/>
    /// </summary>
    /// <param name="cacheDir">The cache directory to use.</param>
    public static void SetCacheDirSkipGC(string? cacheDir) => Shared.SetCacheDirSkipGC(cacheDir);

    /// <summary>
    /// Gets the cached response status from the given URL using HEAD request. The return value: <see cref="UrlStatus"/> contains the HTTP status code and size of the response.
    /// </summary>
    /// <param name="client">Client to be used to retrieve the response.</param>
    /// <param name="url">The URL to check</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The return status of the given URL.</returns>
    public static ValueTask<UrlStatus> GetCachedUrlStatus(
        this HttpClient   client,
        string            url,
        CancellationToken token)
        => Shared.GetCachedUrlStatus(client, new Uri(url), token);

    /// <summary>
    /// Gets the cached response status from the given URL using HEAD request. The return value: <see cref="UrlStatus"/> contains the HTTP status code and size of the response.
    /// </summary>
    /// <param name="client">Client to be used to retrieve the response.</param>
    /// <param name="url">The URL to check</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The return status of the given URL.</returns>
    public static ValueTask<UrlStatus> GetCachedUrlStatus(
        this HttpClient   client,
        Uri               url,
        CancellationToken token)
        => Shared.GetCachedUrlStatus(client, url, token);

    /// <summary>
    /// This method is used to perform a cache cleanup up of the current cache directory based on <see cref="MaxAcceptedCacheExpireTime"/>.<br/>
    /// It's kind of expensive but this requires synchronous operation to make sure that the cache directory is cleaned up properly and avoid any possible race-condition issue.<br/>
    /// </summary>
    /// <param name="cachePath">The cache path to clean-up.</param>
    /// <param name="forceClean">Remove all caches even though the file isn't expired yet.</param>
    /// <returns>Current cache path used by the instance.</returns>
    public static string? PerformCacheGarbageCollection(
        string? cachePath,
        bool    forceClean = false)
        => Shared.PerformCacheGarbageCollection(cachePath, forceClean);

    /// <summary>
    /// Gets cached JSON response from a URL using a specified HTTP methods.
    /// </summary>
    /// <typeparam name="T">The type of instance to be deserialized.</typeparam>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="url">URL source for the response.</param>
    /// <param name="jsonTypeInfo">The JSON Type Info for deserializing JSON response to the target instance type.</param>
    /// <param name="httpMethod">HTTP Method used for getting the response.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The deserialized response from the JSON.</returns>
    public static Task<T?> GetFromCachedJsonAsync<T>(
        this HttpClient   client,
        string?           url,
        JsonTypeInfo<T?>  jsonTypeInfo,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
        where T : class
        => Shared.GetFromCachedJsonAsync(client,
                                         url,
                                         jsonTypeInfo,
                                         httpMethod,
                                         token);

    /// <summary>
    /// Gets cached JSON response from a URL using a specified HTTP request message.
    /// </summary>
    /// <typeparam name="T">The type of instance to be deserialized.</typeparam>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="requestMessage">The request message to be sent to the server.</param>
    /// <param name="jsonTypeInfo">The JSON Type Info for deserializing JSON response to the target instance type.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The deserialized response from the JSON.</returns>
    public static Task<T?> GetFromCachedJsonAsync<T>(
        this HttpClient    client,
        HttpRequestMessage requestMessage,
        JsonTypeInfo<T?>   jsonTypeInfo,
        CancellationToken  token = default)
        where T : class
        => Shared.GetFromCachedJsonAsync(client,
                                         requestMessage,
                                         jsonTypeInfo,
                                         token);

    /// <summary>
    /// Gets cached string response using a specified HTTP methods.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="url">URL source for the response.</param>
    /// <param name="httpMethod">HTTP Method used for getting the response.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as a string.</returns>
    public static Task<string> GetFromCachedStringAsync(
        this HttpClient   client,
        string?           url,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
        => Shared.GetFromCachedStringAsync(client,
                                           url,
                                           httpMethod,
                                           token);

    /// <summary>
    /// Gets cached string response using a specified HTTP request message.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="requestMessage">The request message to be sent to the server.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as a string.</returns>
    public static Task<string> GetFromCachedStringAsync(
        this HttpClient    client,
        HttpRequestMessage requestMessage,
        CancellationToken  token = default)
        => Shared.GetFromCachedStringAsync(client,
                                           requestMessage,
                                           token);

    /// <summary>
    /// Gets cached response as a Stream using a specified HTTP methods.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="url">URL source for the response.</param>
    /// <param name="httpMethod">HTTP Method used for getting the response.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as an instance of a Stream.</returns>
    public static ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        this HttpClient   client,
        string            url,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
        => Shared.TryGetCachedStreamFrom(client,
                                         url,
                                         httpMethod,
                                         token);

    /// <summary>
    /// Gets cached response as a Stream using a specified HTTP methods.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="url">URL source for the response.</param>
    /// <param name="httpMethod">HTTP Method used for getting the response.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as an instance of a Stream.</returns>
    public static ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        this HttpClient   client,
        Uri               url,
        HttpMethod?       httpMethod = null,
        CancellationToken token      = default)
        => Shared.TryGetCachedStreamFrom(client,
                                         url,
                                         httpMethod,
                                         token);

    /// <summary>
    /// Gets cached response as a Stream using a specified HTTP request message.
    /// </summary>
    /// <param name="client">HTTP Client instance to be used.</param>
    /// <param name="requestMessage">The request message to be sent to the server.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as an instance of a Stream.</returns>
    public static ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        this HttpClient    client,
        HttpRequestMessage requestMessage,
        CancellationToken  token = default)
        => Shared.TryGetCachedStreamFrom(client,
                                         requestMessage,
                                         token);

    /// <summary>
    /// Gets cached response as a Stream using an existing HTTP response message.
    /// </summary>
    /// <param name="response">An existing HTTP response message.</param>
    /// <param name="token">Cancellation token for the async operation.</param>
    /// <returns>The response as an instance of a Stream.</returns>
    public static ValueTask<CDNCacheResult> TryGetCachedStreamFrom(
        this HttpResponseMessage? response,
        CancellationToken         token = default)
        => Shared.TryGetCachedStreamFrom(response,
                                         token);
}

