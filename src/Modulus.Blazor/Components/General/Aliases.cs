// Modulus aliases for Ant Design Blazor general components.
//
// Each App* type IS the AntDesign component (subclass, no re-declared parameters),
// so every upstream parameter, event and child API works unchanged and new
// upstream parameters appear automatically on upgrade. Add Modulus-wide defaults
// by overriding OnInitialized in the alias — never by re-declaring a [Parameter].

using Microsoft.AspNetCore.Components;

namespace Modulus.Blazor.Components.General;

public class AppButton : AntDesign.Button { }

public class AppIcon : AntDesign.Icon { }

public class AppBackTop : AntDesign.BackTop { }

public class AppWatermark : AntDesign.Watermark { }

public class AppTitle : AntDesign.Title { }

public class AppText : AntDesign.Text { }

public class AppParagraph : AntDesign.Paragraph { }

public class AppLink : AntDesign.Link { }
