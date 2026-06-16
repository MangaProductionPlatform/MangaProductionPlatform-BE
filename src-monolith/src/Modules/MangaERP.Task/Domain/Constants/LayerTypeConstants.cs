namespace MangaERP.Task.Domain.Constants;

public static class LayerTypeConstants
{
    public const string LineArt = "LineArt";
    public const string Background = "Background";
    public const string Coloring = "Coloring";
    public const string Text = "Text";
    public const string Effects = "Effects";
    public const string Dialogue = "Dialogue";

    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        LineArt, Background, Coloring, Text, Effects, Dialogue
    };

    public static bool IsValid(string layerType)
        => !string.IsNullOrWhiteSpace(layerType) && ValidTypes.Contains(layerType);

    public static IReadOnlyCollection<string> All => ValidTypes.ToList().AsReadOnly();
}
