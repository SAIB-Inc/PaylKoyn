using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace PaylKoyn.ImageGen.Utils;

public static class ImageUtil
{
    public static byte[] CombineImages(string[] imagePaths, int width = 512, int height = 512, string? outputPath = null)
    {
        if (imagePaths == null || imagePaths.Length == 0)
            throw new ArgumentException("At least one image path is required");

        // Load the base image (first one)
        using Image baseImage = Image.Load(imagePaths[0]);

        // Layer each subsequent image on top
        baseImage.Mutate(ctx =>
        {
            for (int i = 1; i < imagePaths.Length; i++)
            {
                using Image layerImage = Image.Load(imagePaths[i]);
                ctx.DrawImage(layerImage, Point.Empty, 1.0f);
            }
            ctx.Resize(width, height);
        });

        // Convert to byte array
        using MemoryStream memoryStream = new();
        PngEncoder encoder = new()
        {
            ColorType = PngColorType.RgbWithAlpha,
            CompressionLevel = PngCompressionLevel.BestCompression
        };

        baseImage.Save(memoryStream, encoder);
        byte[] imageBytes = memoryStream.ToArray();

        // Optionally save to file if path is provided
        if (!string.IsNullOrEmpty(outputPath))
        {
            File.WriteAllBytes(outputPath, imageBytes);
        }

        return imageBytes;
    }
}