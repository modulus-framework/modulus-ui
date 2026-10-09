using Fluid;

namespace Modulus.AspNetCore.Fluid;

/// <summary>Settings of <see cref="ModulusLiquid"/>.</summary>
public sealed class ModulusLiquidOptions
{
    /// <summary>Where the Modulus assets are served from: the <c>_content/Modulus.Ui.Assets</c> prefix of the UI package.</summary>
    public string AssetPrefix { get; set; } = "/_content/Modulus.Ui.Assets/";

    /// <summary>Where the Ant Design styles are served from: the <c>_content/AntDesign</c> prefix.</summary>
    public string AntDesignPrefix { get; set; } = "/_content/AntDesign/";

    /// <summary>
    /// The Fluid options: the filters, and the member access that decides which model properties a template may read. Member
    /// access is closed by default; register a model type with <see cref="Register{T}"/> to let its public properties through.
    /// </summary>
    public TemplateOptions Template { get; } = new();

    /// <summary>Lets templates read the public properties of <typeparamref name="T"/> (a model type, not a dictionary).</summary>
    public ModulusLiquidOptions Register<T>()
    {
        Template.MemberAccessStrategy.Register<T>();
        return this;
    }
}
