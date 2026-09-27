using System.Text;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>
/// Fixed-format input (tax ids, postal codes, account numbers, part numbers). In a mask, <c>9</c> takes a digit,
/// <c>A</c> a letter, <c>*</c> a letter or digit (letters are upper-cased), <c>\</c> makes the next character
/// literal, and every other character is a literal the field types for you. Mirrored by <c>mMask</c> in
/// modulus-widgets.js.
/// </summary>
public static class InputMask
{
    private readonly record struct Slot(char Kind, char Literal)
    {
        public bool IsLiteral => Kind == 'L';
    }

    private static List<Slot> Slots(string mask)
    {
        var slots = new List<Slot>(mask.Length);
        for (var i = 0; i < mask.Length; i++)
        {
            var c = mask[i];
            if (c == '\\' && i + 1 < mask.Length) slots.Add(new('L', mask[++i]));
            else if (c is '9' or 'A' or '*') slots.Add(new(c, '\0'));
            else slots.Add(new('L', c));
        }
        return slots;
    }

    private static bool Accepts(Slot slot, char c) => slot.Kind switch
    {
        '9' => char.IsAsciiDigit(c),
        'A' => char.IsAsciiLetter(c),
        '*' => char.IsAsciiLetterOrDigit(c),
        _ => false,
    };

    /// <summary>
    /// Fits typed text to the mask: characters that don't fit a slot are dropped, literals are inserted as the
    /// next slot fills (typing the literal itself also works), and input stops at the end of the mask.
    /// </summary>
    public static string Apply(string mask, string? text)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(mask)) return "";
        var slots = Slots(mask);
        var sb = new StringBuilder(slots.Count);
        var t = 0;
        foreach (var c in text)
        {
            if (t >= slots.Count) break;
            var k = t;
            var pending = new StringBuilder();
            var typedLiteral = false;
            while (k < slots.Count && slots[k].IsLiteral)
            {
                if (c == slots[k].Literal)
                {
                    sb.Append(pending).Append(c);
                    t = k + 1;
                    typedLiteral = true;
                    break;
                }
                pending.Append(slots[k].Literal);
                k++;
            }
            if (typedLiteral || k >= slots.Count || !Accepts(slots[k], c)) continue;
            sb.Append(pending).Append(slots[k].Kind == '9' ? c : char.ToUpperInvariant(c));
            t = k + 1;
        }
        return sb.ToString();
    }

    /// <summary>The mask with each slot shown as <paramref name="blank"/>: "99-9999999" → "__-_______".</summary>
    public static string Placeholder(string mask, char blank = '_') =>
        string.Concat(Slots(mask).Select(s => s.IsLiteral ? s.Literal : blank));

    /// <summary>True when every slot is filled.</summary>
    public static bool IsComplete(string mask, string? value) =>
        !string.IsNullOrEmpty(value) && Apply(mask, value) == value && value.Length == Slots(mask).Count;

    /// <summary>Just the characters typed into slots, without the literals: "12-3456789" → "123456789".</summary>
    public static string Raw(string mask, string? value)
    {
        var formatted = Apply(mask, value);
        var slots = Slots(mask);
        var sb = new StringBuilder(formatted.Length);
        for (var i = 0; i < formatted.Length && i < slots.Count; i++)
            if (!slots[i].IsLiteral) sb.Append(formatted[i]);
        return sb.ToString();
    }
}
