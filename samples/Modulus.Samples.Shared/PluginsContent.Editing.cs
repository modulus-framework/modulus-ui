namespace Modulus.Samples.Shared;

/// <summary>Content of the Plugins pages for the image cropper and the code editor, shared by the three samples.</summary>
public static partial class PluginsContent
{
    /// <summary>The picture the cropper sample crops: a gallery photo, drawn as an image data URI.</summary>
    public static string CropPicture => ExtraContent.Photos[0].Src;

    /// <summary>The JSON the code editor sample opens on: an approval rule with its thresholds.</summary>
    public const string SampleRuleJson = """
        {
          "rule": "purchase-approval",
          "threshold": 5000,
          "approvers": ["finance", "ops"],
          "tiers": { "gold": 10000, "silver": 5000 }
        }
        """;

    /// <summary>The JavaScript the code editor sample opens on: an order total with tax.</summary>
    public const string SampleTotalScript = """
        // The total of an order: its lines less the discount, with tax on what is left.
        export function total(lines, discount = 0, taxRate = 0.2) {
          const net = lines.reduce((sum, line) => sum + line.qty * line.price, 0) - discount;
          return Math.round(net * (1 + taxRate) * 100) / 100;
        }
        """;
}
