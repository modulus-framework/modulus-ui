// Modulus aliases for Ant Design Blazor data entry components.
//
// Each App* type IS the AntDesign component (subclass, no re-declared parameters),
// so every upstream parameter, event and child API works unchanged and new
// upstream parameters appear automatically on upgrade. Add Modulus-wide defaults
// by overriding OnInitialized in the alias — never by re-declaring a [Parameter].

using Microsoft.AspNetCore.Components;

namespace Modulus.Blazor.Components.DataEntry;

[CascadingTypeParameter("TOption")]
public class AppAutoComplete<TOption> : AntDesign.AutoComplete<TOption> { }

public class AppCascader : AntDesign.Cascader { }

public class AppCheckbox : AntDesign.Checkbox { }

[CascadingTypeParameter("TValue")]
public class AppCheckboxGroup<TValue> : AntDesign.CheckboxGroup<TValue> { }

public class AppDatePicker<TValue> : AntDesign.DatePicker<TValue> { }

public class AppRangePicker<TValue> : AntDesign.RangePicker<TValue> { }

public class AppTimePicker<TValue> : AntDesign.TimePicker<TValue> { }

[CascadingTypeParameter("TModel")]
public class AppForm<TModel> : AntDesign.Form<TModel> { }

public class AppFormItem : AntDesign.FormItem { }

public class AppInput<TValue> : AntDesign.Input<TValue> { }

public class AppInputGroup : AntDesign.InputGroup { }

public class AppInputPassword : AntDesign.InputPassword { }

public class AppSearch : AntDesign.Search { }

public class AppTextArea : AntDesign.TextArea { }

public class AppInputNumber<TValue> : AntDesign.InputNumber<TValue> { }

public class AppMentions : AntDesign.Mentions { }

public class AppRadio<TValue> : AntDesign.Radio<TValue> { }

[CascadingTypeParameter("TValue")]
public class AppRadioGroup<TValue> : AntDesign.RadioGroup<TValue> { }

/// <summary>Rate that defaults to the filled star glyph, matching antd v5.</summary>
public class AppRate : AntDesign.Rate
{
    protected override void OnInitialized()
    {
        Character ??= _ => builder =>
        {
            builder.OpenComponent<AntDesign.Icon>(0);
            builder.AddAttribute(1, nameof(AntDesign.Icon.Type), "star");
            builder.AddAttribute(2, nameof(AntDesign.Icon.Theme), AntDesign.IconThemeType.Fill);
            builder.CloseComponent();
        };
        base.OnInitialized();
    }
}

[CascadingTypeParameter("TItemValue")]
public class AppSelect<TItemValue, TItem> : AntDesign.Select<TItemValue, TItem> { }

public class AppSlider<TValue> : AntDesign.Slider<TValue> { }

public class AppSwitch : AntDesign.Switch { }

public class AppTransfer : AntDesign.Transfer { }

[CascadingTypeParameter("TItemValue")]
public class AppTreeSelect<TItemValue, TItem> : AntDesign.TreeSelect<TItemValue, TItem> { }

public class AppUpload : AntDesign.Upload { }
