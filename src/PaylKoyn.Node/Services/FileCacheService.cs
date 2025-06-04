using System.Collections.Concurrent;
using System.Text.Json;

namespace PaylKoyn.Node.Services;

public record CacheMetadata(string ContentType, string FileName, long FileSize, DateTime CachedAt);

public class FileCacheService
{
    private readonly string _cacheDirectory;
    private readonly long _maxCacheSizeBytes;
    private readonly ILogger<FileCacheService> _logger;
    private readonly ConcurrentDictionary<string, (string filePath, long fileSize)> _cache = new();

    public FileCacheService(IConfiguration configuration, ILogger<FileCacheService> logger)
    {
        _cacheDirectory = configuration["FileCache:CacheDirectory"] ?? "/tmp/paylkoyn-cache";
        _maxCacheSizeBytes = long.Parse(configuration["FileCache:MaxCacheSizeBytes"] ?? "1073741824"); // 1GB
        _logger = logger;

        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public async Task<(byte[] fileBytes, string contentType, string fileName)?> GetCachedFileAsync(string hash)
    {
        if (!_cache.TryGetValue(hash, out (string filePath, long fileSize) cacheItem))
        {
            return null;
        }

        string metadataPath = Path.Combine(_cacheDirectory, $"{hash}.meta");
        if (!File.Exists(cacheItem.filePath) || !File.Exists(metadataPath))
        {
            _cache.TryRemove(hash, out _);
            return null;
        }

        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(cacheItem.filePath);
            string metadataJson = await File.ReadAllTextAsync(metadataPath);
            CacheMetadata? metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataJson);

            return (fileBytes, metadata?.ContentType ?? "application/octet-stream", metadata?.FileName ?? $"file_{hash}");
        }
        catch
        {
            return null;
        }
    }

    public async Task CacheFileAsync(string hash, byte[] fileBytes, string contentType, string fileName)
    {
        if (_cache.ContainsKey(hash))
        {
            return;
        }

        // Check if we need to free space
        long currentSize = GetCurrentCacheSize();
        if (currentSize + fileBytes.Length > _maxCacheSizeBytes)
        {
            FreeOldestFiles(fileBytes.Length);
        }

        string filePath = Path.Combine(_cacheDirectory, $"{hash}.cache");
        string metadataPath = Path.Combine(_cacheDirectory, $"{hash}.meta");

        try
        {
            // Save file content
            await File.WriteAllBytesAsync(filePath, fileBytes);

            // Save metadata
            CacheMetadata metadata = new CacheMetadata(contentType, fileName, fileBytes.Length, DateTime.UtcNow);
            string metadataJson = JsonSerializer.Serialize(metadata);
            await File.WriteAllTextAsync(metadataPath, metadataJson);

            _cache.TryAdd(hash, (filePath, fileBytes.Length));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching file: {Hash}", hash);

            // Clean up on error
            if (File.Exists(filePath)) File.Delete(filePath);
            if (File.Exists(metadataPath)) File.Delete(metadataPath);
        }
    }

    private void FreeOldestFiles(long requiredSpace)
    {
        List<FileInfo> files = Directory.GetFiles(_cacheDirectory, "*.cache")
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.LastAccessTime)
            .ToList();

        long freedSpace = 0;
        foreach (FileInfo? file in files)
        {
            if (freedSpace >= requiredSpace)
                break;

            try
            {
                string hash = Path.GetFileNameWithoutExtension(file.Name);
                _cache.TryRemove(hash, out _);
                file.Delete();

                // Also delete metadata file
                string metadataPath = Path.Combine(_cacheDirectory, $"{hash}.meta");
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                freedSpace += file.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting cache file: {FileName}", file.Name);
            }
        }
    }

    private long GetCurrentCacheSize()
    {
        return _cache.Values.Sum(x => x.fileSize);
    }
}