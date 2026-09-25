// Modulus aliases for Ant Design Blazor feedback components.
//
// Each App* type IS the AntDesign component (subclass, no re-declared parameters),
// so every upstream parameter, event and child API works unchanged and new
// upstream parameters appear automatically on upgrade. Add Modulus-wide defaults
// by overriding OnInitialized in the alias — never by re-declaring a [Parameter].

using Microsoft.AspNetCore.Components;

namespace Modulus.Blazor.Components.Feedback;

public class AppAlert : AntDesign.Alert { }

public class AppDrawer : AntDesign.Drawer { }

public class AppModal : AntDesign.Modal { }

public class AppPopconfirm : AntDesign.Popconfirm { }

public class AppProgress : AntDesign.Progress { }

public class AppResult : AntDesign.Result { }

public class AppSkeleton : AntDesign.Skeleton { }

public class AppSpin : AntDesign.Spin { }
