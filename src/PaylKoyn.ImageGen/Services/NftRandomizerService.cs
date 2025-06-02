using Paylkoyn.ImageGen.Utils;

namespace Paylkoyn.ImageGen.Services;

public record NftConfig(List<AttributeGroup> AttributeGroups, Dictionary<string, (int IncludeWeight, int NoneWeight)> OptionalCategories);
public record AttributeGroup(string Name, int Weight, string[] Categories, string? FileName = null);
public record TraitLayer(int Layer, string FileName);
public record NftTrait(int Layer, string Category, string TraitName);

public class NftRandomizerService
{
    private static readonly NftConfig _config = new(
        AttributeGroups: [
            new("Pinky Pie", 3, ["background", "body", "clothing", "eyes", "hat"]),
            new("Pyro", 10, ["background", "body", "clothing", "eyes", "hat"]),
            new("Rocker", 10, ["background", "body", "clothing", "eyes", "hat"]),
            new("Zest", 10, ["background", "body", "eyes", "hat"]),
            new("Base", 15, ["background", "body", "eyes", "lineart"])
        ],
        OptionalCategories: new Dictionary<string, (int, int)>
        {
            ["clothing"] = (1, 1),
            ["hat"] = (1, 1)
        }
    );

    private const string BasePath = "./Assets";
    private static readonly Random _random = Random.Shared;

    public byte[] GenerateRandomNFT(IEnumerable<NftTrait> traits, string? outputPath = null)
    {
        List<string> imagePaths = [];

        foreach (NftTrait? trait in traits.OrderBy(t => t.Layer))
        {
            string fullPath = BuildFilePath(trait.Category, trait.TraitName);
            if (!string.IsNullOrEmpty(fullPath))
            {
                imagePaths.Add(fullPath);
            }
        }

        return ImageUtil.CombineImages([.. imagePaths], outputPath);
    }

    public IEnumerable<NftTrait> GenerateRandomTraits()
    {
        List<NftTrait> selectedTraits = [];

        // Define all possible categories (including lineart now)
        string[] allCategories = ["background", "body", "lineart", "clothing", "eyes", "hat"];

        foreach (string category in allCategories)
        {
            if (ShouldSkipOptionalCategory(category))
            {
                continue;
            }

            List<AttributeGroup> availableGroups = [.. _config.AttributeGroups.Where(g => g.Categories.Contains(category))];

            if (availableGroups.Count > 0)
            {
                AttributeGroup selectedGroup = SelectRandomAttributeGroup(availableGroups);

                selectedTraits.Add(new NftTrait(
                    Layer: GetLayerNumber(category),
                    Category: category,
                    TraitName: selectedGroup.Name
                ));
            }
        }

        return selectedTraits.OrderBy(t => t.Layer);
    }

    private static string ConvertToKebabCase(string name) => name.ToLowerInvariant().Replace(' ', '-');

    private static bool ShouldSkipOptionalCategory(string category)
    {
        if (!_config.OptionalCategories.TryGetValue(category, out (int IncludeWeight, int NoneWeight) weights))
            return false; // Not optional, always include

        int totalWeight = weights.IncludeWeight + weights.NoneWeight;
        int randomValue = _random.Next(1, totalWeight + 1);

        // If random falls within NoneWeight range (from IncludeWeight+1 to total), skip
        return randomValue > weights.IncludeWeight;
    }

    private static AttributeGroup SelectRandomAttributeGroup(List<AttributeGroup> availableGroups)
    {
        int totalWeight = availableGroups.Sum(g => g.Weight);
        int randomValue = _random.Next(1, totalWeight + 1);

        int cumulativeWeight = 0;
        foreach (AttributeGroup group in availableGroups)
        {
            cumulativeWeight += group.Weight;
            if (randomValue <= cumulativeWeight)
            {
                return group;
            }
        }

        return availableGroups.First();
    }

    private static string BuildFilePath(string category, string traitName)
    {
        AttributeGroup? attributeGroup = _config.AttributeGroups.FirstOrDefault(g => g.Name == traitName);
        if (attributeGroup != null && attributeGroup.Categories.Contains(category))
        {
            // Use custom filename or convert name to kebab-case
            string fileName = attributeGroup.FileName ?? ConvertToKebabCase(attributeGroup.Name);
            return $"{BasePath}/{category}/{category}-{fileName}.png";
        }

        return string.Empty;
    }

    private static int GetLayerNumber(string category) => category switch
    {
        "background" => 1,
        "body" => 2,
        "lineart" => 3,
        "clothing" => 4,
        "eyes" => 5,
        "hat" => 6,
        _ => 99
    };
}