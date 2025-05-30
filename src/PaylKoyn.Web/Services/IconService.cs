namespace PaylKoyn.Web.Services;

public class IconService(IWebHostEnvironment env)
{
    private readonly Dictionary<string, string> _cache = [];

    public string DeleteIcon => _cache.TryGetValue("delete.svg", out string? value) ? value : LoadIcon("delete.svg");
    public string ImageIcon => _cache.TryGetValue("image.svg", out string? value) ? value : LoadIcon("image.svg");
    public string LoadingIcon => _cache.TryGetValue("loading.svg", out string? value) ? value : LoadIcon("loading.svg");
    public string QrIcon => _cache.TryGetValue("qr.svg", out string? value) ? value : LoadIcon("qr.svg");
    public string UploadIcon => _cache.TryGetValue("upload.svg", out string? value) ? value : LoadIcon("upload.svg");
    public string LoadIcon(string iconName)
    {
        string iconPath = Path.Combine(env.WebRootPath, "images", "icons", iconName);
        if (File.Exists(iconPath))
        {
            _cache[iconName] = File.ReadAllText(iconPath);
        }
        return _cache[iconName];
    }
}