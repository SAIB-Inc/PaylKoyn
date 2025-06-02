using Paylkoyn.ImageGen.Utils;

namespace Paylkoyn.ImageGen.Services;

public record NftConfig(Dictionary<string, TraitCategory> Categories);

public record TraitCategory(int Layer, List<TraitOption> Options);

public record TraitOption(string Name, string Path, double Chance);

public record NftTrait(int Layer, string Category, string TraitName);

public class NftRandomizerService(IConfiguration config)
{
    private readonly NftConfig _config = new(config.GetSection("NftTraits").Get<Dictionary<string, TraitCategory>>() ?? []);
    private readonly Random _random = new();

    public IEnumerable<NftTrait> GenerateRandomTraits()
    {
        List<NftTrait> selectedTraits = [];

        // Sort categories by layer to ensure proper ordering
        List<KeyValuePair<string, TraitCategory>> sortedCategories = [.. _config.Categories.OrderBy(kvp => kvp.Value.Layer)];

        // Go through each category in layer order
        foreach (KeyValuePair<string, TraitCategory> category in sortedCategories)
        {
            TraitOption? selectedTrait = SelectRandomTrait(category.Value.Options);
            if (selectedTrait != null) selectedTraits.Add(new NftTrait(
                Layer: category.Value.Layer,
                Category: category.Key,
                TraitName: selectedTrait.Name
            ));
        }

        return selectedTraits;
    }

    public byte[] GenerateRandomNFT(IEnumerable<NftTrait> traits, string? outputPath = null)
    {
        List<string> imagePaths = [];

        // Go through each sorted trait
        foreach (NftTrait trait in traits.OrderBy(t => t.Layer))
        {
            // Find the corresponding category
            if (_config.Categories.TryGetValue(trait.Category, out TraitCategory? category))
            {
                // Find the trait option by name
                TraitOption? option = category.Options.FirstOrDefault(o => o.Name == trait.TraitName);
                if (option != null)
                {
                    imagePaths.Add(option.Path); // Add the image path to the list
                }
            }
        }

        return ImageUtil.CombineImages([.. imagePaths], outputPath);
    }

    private TraitOption? SelectRandomTrait(List<TraitOption> traitOptions)
    {
        if (traitOptions.Count == 0) return null;

        // Calculate total probability
        double totalProbability = traitOptions.Sum(t => t.Chance);

        if (totalProbability > 100.0)
            throw new ArgumentException($"Total probability exceeds 100% ({totalProbability:F2}%)");

        // Generate random number (0.0-100.0)
        double randomValue = _random.NextDouble() * 100.0;

        // Check each trait option
        double cumulativeProbability = 0.0;
        foreach (TraitOption trait in traitOptions)
        {
            cumulativeProbability += trait.Chance;
            if (randomValue <= cumulativeProbability)
            {
                return trait; // This trait was selected
            }
        }

        // No trait selected (falls in the "no trait" range)
        return null;
    }
}