using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace Paylkoyn.ImageGen.Utils;

public static class ImageUtil
{
    public static byte[] CombineImages(string[] imagePaths, string? outputPath = null)
    {
        if (imagePaths == null || imagePaths.Length == 0)
            throw new ArgumentException("At least one image path is required");

        // Load the base image (first one)
        using var baseImage = Image.Load(imagePaths[0]);

        // Layer each subsequent image on top
        baseImage.Mutate(ctx =>
        {
            for (int i = 1; i < imagePaths.Length; i++)
            {
                using var layerImage = Image.Load(imagePaths[i]);
                // Overlay at (0,0) since all images are same size squares
                ctx.DrawImage(layerImage, Point.Empty, 1.0f);
            }
        });

        // Convert to byte array
        using var memoryStream = new MemoryStream();
        var encoder = new PngEncoder()
        {
            ColorType = PngColorType.RgbWithAlpha
        };

        baseImage.Save(memoryStream, encoder);
        var imageBytes = memoryStream.ToArray();

        // Optionally save to file if path is provided
        if (!string.IsNullOrEmpty(outputPath))
        {
            File.WriteAllBytes(outputPath, imageBytes);
        }

        return imageBytes;
    }
}