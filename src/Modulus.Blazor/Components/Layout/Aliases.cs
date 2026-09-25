// Modulus aliases for Ant Design Blazor layout components.
//
// Each App* type IS the AntDesign component (subclass, no re-declared parameters),
// so every upstream parameter, event and child API works unchanged and new
// upstream parameters appear automatically on upgrade. Add Modulus-wide defaults
// by overriding OnInitialized in the alias — never by re-declaring a [Parameter].

using Microsoft.AspNetCore.Components;

namespace Modulus.Blazor.Components.Layout;

public class AppDivider : AntDesign.Divider { }

public class AppFlex : AntDesign.Flex { }

public class AppRow : AntDesign.Row { }

public class AppCol : AntDesign.Col { }

public class AppLayout : AntDesign.Layout { }

public class AppHeader : AntDesign.Header { }

public class AppSider : AntDesign.Sider { }

public class AppContent : AntDesign.Content { }

public class AppFooter : AntDesign.Footer { }

public class AppSpace : AntDesign.Space { }

public class AppSpaceItem : AntDesign.SpaceItem { }

public class AppSplitter : AntDesign.Splitter { }

public class AppAffix : AntDesign.Affix { }
