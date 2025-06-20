namespace PaylKoyn.Web.Services;

public class IconService(IWebHostEnvironment env)
{
    private readonly Dictionary<string, string> _cache = [];

    public string CheckIcon => _cache.TryGetValue("check.svg", out string? value) ? value : LoadIcon("check.svg");
    public string CopyIcon => _cache.TryGetValue("copy.svg", out string? value) ? value : LoadIcon("copy.svg");
    public string DeleteIcon => _cache.TryGetValue("delete.svg", out string? value) ? value : LoadIcon("delete.svg");
    public string FacebookIcon => _cache.TryGetValue("facebook.svg", out string? value) ? value : LoadIcon("facebook.svg");
    public string GithubIcon => _cache.TryGetValue("github.svg", out string? value) ? value : LoadIcon("github.svg");
    public string ImageIcon => _cache.TryGetValue("image.svg", out string? value) ? value : LoadIcon("image.svg");
    public string LinkedInIcon => _cache.TryGetValue("linkedin.svg", out string? value) ? value : LoadIcon("linkedin.svg");
    public string LogoutIcon => _cache.TryGetValue("logout.svg", out string? value) ? value : LoadIcon("logout.svg");
    public string LoadingIcon => _cache.TryGetValue("loading.svg", out string? value) ? value : LoadIcon("loading.svg");
    public string PaylIcon => _cache.TryGetValue("payl.svg", out string? value) ? value : LoadIcon("payl.svg");
    public string QrIcon => _cache.TryGetValue("qr.svg", out string? value) ? value : LoadIcon("qr.svg");
    public string UploadIcon => _cache.TryGetValue("upload.svg", out string? value) ? value : LoadIcon("upload.svg");
    public string WalletIcon => _cache.TryGetValue("wallet.svg", out string? value) ? value : LoadIcon("wallet.svg");
    public string XIcon => _cache.TryGetValue("x.svg", out string? value) ? value : LoadIcon("x.svg");

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