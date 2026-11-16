using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

public sealed partial class TableQuery<T>
{
    // Cursor -----------------------------------------------------------------------------------------------------------

    // [field, descending, sort value, id, offset] as base64url JSON. It only ever becomes typed values in a WHERE, never SQL text.
    private sealed record Cursor(string Field, bool Descending, string? Value, string? Id, int Offset)
    {
        private const int MaxLength = 1024;

        // single: the one sort whose value keyset paging continues after (null for several sorts, which page by offset).
        public static string For(string field, Sorting? single, bool descending, Key id, T last, int offset)
        {
            var value = single is not { } sort || ReferenceEquals(sort.Key, id) ? null : sort.Key.Text(sort.Key.Read(last));
            var json = JsonSerializer.Serialize(new object?[] { field, descending ? 1 : 0, value, id.Text(id.Read(last)), offset });
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static Cursor? Parse(string text)
        {
            if (text.Length > MaxLength) return null;
            try
            {
                var base64 = text.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                using var doc = JsonDocument.Parse(Convert.FromBase64String(base64));
                var a = doc.RootElement;
                if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 5) return null;
                var offset = a[4].GetInt32();
                return offset < 0 ? null : new Cursor(a[0].GetString() ?? "", a[1].GetInt32() == 1, Str(a[2]), Str(a[3]), offset);
            }
            catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException)
            {
                return null;
            }

            static string? Str(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetString();
        }
    }
}
