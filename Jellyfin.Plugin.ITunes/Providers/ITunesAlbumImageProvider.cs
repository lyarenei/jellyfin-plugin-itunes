using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ITunes.MetadataServices;
using Jellyfin.Plugin.ITunes.Utils;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using IMetadataService = Jellyfin.Plugin.ITunes.MetadataServices.IMetadataService;

namespace Jellyfin.Plugin.ITunes.Providers;

/// <summary>
/// The iTunes album image provider.
/// </summary>
public class ITunesAlbumImageProvider : IRemoteImageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ITunesAlbumImageProvider> _logger;
    private readonly IMetadataService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ITunesAlbumImageProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <param name="service">Metadata service provider. If null, a default instance will be used.</param>
    public ITunesAlbumImageProvider(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, IMetadataService? service = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<ITunesAlbumImageProvider>();
        _service = service ?? new AppleMusicMetadataService(loggerFactory);
    }

    /// <inheritdoc />
    public string Name => PluginUtils.PluginName;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is MusicAlbum;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return new List<ImageType> { ImageType.Primary };
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
        return await httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not MusicAlbum album)
        {
            _logger.LogDebug("Provided item is not an album, cannot continue");
            return new List<RemoteImageInfo>();
        }

        if (string.IsNullOrEmpty(album.Name))
        {
            _logger.LogInformation("No album name provided, cannot continue");
            return new List<RemoteImageInfo>();
        }

        var searchTerm = $"{album.AlbumArtist} {album.Name}";
        var albumUrls = await _service.Search(searchTerm, ItemType.Album, cancellationToken).ConfigureAwait(false);
        var albumUrlList = albumUrls.ToList();
        if (!albumUrlList.Any())
        {
            _logger.LogInformation("No albums found for {Term}", searchTerm);
            return new List<RemoteImageInfo>();
        }

        var infos = new List<RemoteImageInfo>();
        foreach (var url in albumUrlList)
        {
            var data = await _service.Scrape(url, ItemType.Album).ConfigureAwait(false);
            if (data?.ImageUrl is null)
            {
                _logger.LogDebug("Failed to scrape data on {Url}", url);
                continue;
            }

            var info = new RemoteImageInfo
            {
                Height = 1200,
                Width = 1200,
                ProviderName = Name,
                ThumbnailUrl = ModifySize(data.ImageUrl, "1200x1200bf", "100x100cc"),
                Type = ImageType.Primary,
                Url = data.ImageUrl
            };

            infos.Add(info);
        }

        return infos;
    }

    private static string ModifySize(string url, string searchSize, string newSize)
    {
        return url.Replace(searchSize, newSize, StringComparison.OrdinalIgnoreCase);
    }
}
