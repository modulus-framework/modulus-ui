namespace Modulus.Ui.Abstractions;

/// <summary>
/// Schema-driven model for ProTable/ProForm across all UI flavors.
/// </summary>
public class ProTableColumn
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DataIndex { get; set; }
    public string? DataType { get; set; } // "text", "number", "date", "select", etc.
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = true;
    public int Width { get; set; } = 150;
    public bool Visible { get; set; } = true;
}

/// <summary>
/// ProTable request model.
/// </summary>
public class ProTableRequest
{
    public int Current { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; } // "ascend" or "descend"
    public Dictionary<string, object?> Filters { get; set; } = new();
}

/// <summary>
/// ProTable response model.
/// </summary>
public class ProTableResponse<T>
{
    public List<T> Data { get; set; } = new();
    public int Total { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}

/// <summary>
/// ProForm field definition.
/// </summary>
public class ProFormField
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? FieldType { get; set; } // "text", "select", "date", "number", etc.
    public bool Required { get; set; }
    public string? Placeholder { get; set; }
    public object? Value { get; set; }
    public List<FieldOption>? Options { get; set; }
    public string? Help { get; set; }
}

/// <summary>
/// Field option for select/radio/checkbox fields.
/// </summary>
public class FieldOption
{
    public string Label { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
}
