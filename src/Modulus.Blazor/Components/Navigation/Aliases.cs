// Modulus aliases for Ant Design Blazor navigation components.
//
// Each App* type IS the AntDesign component (subclass, no re-declared parameters),
// so every upstream parameter, event and child API works unchanged and new
// upstream parameters appear automatically on upgrade. Add Modulus-wide defaults
// by overriding OnInitialized in the alias — never by re-declaring a [Parameter].

using Microsoft.AspNetCore.Components;

namespace Modulus.Blazor.Components.Navigation;

public class AppAnchor : AntDesign.Anchor { }

public class AppBreadcrumb : AntDesign.Breadcrumb { }

public class AppDropdown : AntDesign.Dropdown { }

public class AppDropdownButton : AntDesign.DropdownButton { }

public class AppMenu : AntDesign.Menu { }

public class AppMenuItem : AntDesign.MenuItem { }

public class AppSubMenu : AntDesign.SubMenu { }

public class AppPagination : AntDesign.Pagination { }

public class AppSteps : AntDesign.Steps { }

public class AppStep : AntDesign.Step { }

public class AppTabs : AntDesign.Tabs { }

public class AppTabPane : AntDesign.TabPane { }
