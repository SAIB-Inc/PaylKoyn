using Paylkoyn.ImageGen.Utils;

namespace Paylkoyn.ImageGen.Services;

public record AttributeGroup(string Name, string[] Categories);
public record NftTrait(int Layer, string Category, string TraitName);

public class NftRandomizerService(IConfiguration configuration)
{
    private static readonly List<string> _allCategories = ["background", "body", "lineart", "clothing", "eyes", "hat"];
    private static readonly List<AttributeGroup> _attributeGroups = [
        new("Radioactive Undead", [.. _allCategories.Except(["lineart"])]),
        new("Samurai", [.. _allCategories.Except(["lineart"])]),
        new("Caesar", [.. _allCategories.Except(["lineart"])]),
        new("Pinky Pie", [.. _allCategories.Except(["lineart"])]),
        new("Pyro", [.. _allCategories.Except(["lineart"])]),
        new("Rocker", [.. _allCategories.Except(["lineart"])]),
        new("Zest", [.. _allCategories.Except(["lineart", "clothing"])]),
        new("Base", [.. _allCategories.Except(["clothing", "hat"])]),
    ];
    private static readonly Random _random = Random.Shared;

    private const string BasePath = "./Assets";
    private readonly Dictionary<string, int> _weights = configuration.GetValue<Dictionary<string, int>>("NftWeights") ?? [];

    public byte[] GenerateNftImage(IEnumerable<NftTrait> traits, string? outputPath = null)
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

        foreach (string category in _allCategories)
        {
            if (category == "lineart")
            {
                // Special case: always use Base for lineart
                selectedTraits.Add(new NftTrait(
                    Layer: GetLayerNumber(category),
                    Category: category,
                    TraitName: "Base"
                ));
            }
            else
            {
                // Roll to select any group (regardless of whether they have this category)
                AttributeGroup selectedGroup = SelectRandomAttributeGroup(_attributeGroups);

                // Check if the selected group has this category
                if (selectedGroup.Categories.Contains(category))
                {
                    selectedTraits.Add(new NftTrait(
                        Layer: GetLayerNumber(category),
                        Category: category,
                        TraitName: selectedGroup.Name
                    ));
                }
                // If selected group doesn't have this category = no trait
            }
        }

        return selectedTraits.OrderBy(t => t.Layer);
    }

    private AttributeGroup SelectRandomAttributeGroup(List<AttributeGroup> availableGroups)
    {
        int totalWeight = availableGroups.Sum(g => GetWeight(g.Name));
        int randomValue = _random.Next(1, totalWeight + 1);

        int cumulativeWeight = 0;
        foreach (AttributeGroup group in availableGroups)
        {
            cumulativeWeight += GetWeight(group.Name);
            if (randomValue <= cumulativeWeight)
            {
                return group;
            }
        }

        return availableGroups.First();
    }

    private static string BuildFilePath(string category, string traitName)
    {
        AttributeGroup? attributeGroup = _attributeGroups.FirstOrDefault(g => g.Name == traitName);
        if (attributeGroup != null && attributeGroup.Categories.Contains(category))
        {
            string fileName = ConvertToKebabCase(attributeGroup.Name);
            return $"{BasePath}/{category}/{category}-{fileName}.png";
        }

        return string.Empty;
    }

    private static string ConvertToKebabCase(string name) => name.ToLowerInvariant().Replace(' ', '-');

    private int GetWeight(string groupName) => _weights.GetValueOrDefault(groupName, 1);

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