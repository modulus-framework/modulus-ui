// Modulus aliases for Ant Design Blazor data display components.
//
// Each App* type IS the AntDesign component (subclass, no re-declared parameters),
// so every upstream parameter, event and child API works unchanged and new
// upstream parameters appear automatically on upgrade. Add Modulus-wide defaults
// by overriding OnInitialized in the alias — never by re-declaring a [Parameter].

using Microsoft.AspNetCore.Components;

namespace Modulus.Blazor.Components.DataDisplay;

public class AppAvatar : AntDesign.Avatar { }

[CascadingTypeParameter("TItem")]
public class AppList<TItem> : AntDesign.AntList<TItem> { }

public class AppAvatarGroup : AntDesign.AvatarGroup { }

public class AppBadge : AntDesign.Badge { }

public class AppBadgeRibbon : AntDesign.BadgeRibbon { }

public class AppCalendar : AntDesign.Calendar { }

public class AppCard : AntDesign.Card { }

/// <summary>Carousel with dots on by default, as in Ant Design (AntD Blazor 1.6.2 defaults <c>Dots</c> to false).</summary>
public class AppCarousel : AntDesign.Carousel
{
    public AppCarousel() => Dots = true;
}

public class AppCollapse : AntDesign.Collapse { }

public class AppPanel : AntDesign.Panel { }

public class AppDescriptions : AntDesign.Descriptions { }

public class AppDescriptionsItem : AntDesign.DescriptionsItem { }

public class AppEmpty : AntDesign.Empty { }

public class AppImage : AntDesign.Image { }

public class AppPopover : AntDesign.Popover { }

public class AppSegmented<TValue> : AntDesign.Segmented<TValue> { }

public class AppStatistic<TValue> : AntDesign.Statistic<TValue> { }

public class AppCountDown : AntDesign.CountDown { }

[CascadingTypeParameter("TItem")]
public class AppTable<TItem> : AntDesign.Table<TItem> { }

public class AppTag : AntDesign.Tag { }

public class AppTimeline : AntDesign.Timeline { }

public class AppTimelineItem : AntDesign.TimelineItem { }

public class AppTooltip : AntDesign.Tooltip { }

[CascadingTypeParameter("TItem")]
public class AppTree<TItem> : AntDesign.Tree<TItem> { }
