using System.Globalization;
using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Qr;

/// <summary>QR error correction level: how much of the symbol can be damaged and still read (7, 15, 25, 30%).</summary>
public enum QrErrorLevel { L, M, Q, H }

/// <summary>
/// A QR code symbol (ISO/IEC 18004, byte mode, versions 1–40). Follows Project Nayuki's reference encoder:
/// the smallest version that fits, Reed–Solomon blocks interleaved, and the mask with the lowest penalty.
/// Shared by the Blazor <c>AppQRCode</c> and the MVC <c>app-qr-code</c>, so both draw the same modules.
/// </summary>
public sealed class QrCode
{
    public int Version { get; }
    public int Size { get; }
    public QrErrorLevel ErrorLevel { get; }
    public int Mask { get; }

    private readonly bool[,] _modules;
    private readonly bool[,] _function;

    /// <summary>True when the module at column x, row y is dark.</summary>
    public bool this[int x, int y] => x >= 0 && y >= 0 && x < Size && y < Size && _modules[y, x];

    public static QrCode Encode(string text, QrErrorLevel level = QrErrorLevel.M) =>
        Encode(Encoding.UTF8.GetBytes(text ?? ""), level);

    public static QrCode Encode(byte[] data, QrErrorLevel level = QrErrorLevel.M)
    {
        ArgumentNullException.ThrowIfNull(data);
        for (var version = 1; version <= 40; version++)
        {
            var countBits = version < 10 ? 8 : 16;
            var usedBits = 4 + countBits + data.Length * 8;
            if (data.Length < 1 << countBits && usedBits <= DataCodewords(version, level) * 8)
                return new QrCode(version, level, Codewords(data, version, level, countBits));
        }
        throw new ArgumentException($"Data too long for a QR code at level {level} ({data.Length} bytes).", nameof(data));
    }

    private QrCode(int version, QrErrorLevel level, byte[] dataCodewords)
    {
        Version = version;
        ErrorLevel = level;
        Size = version * 4 + 17;
        _modules = new bool[Size, Size];
        _function = new bool[Size, Size];

        DrawFunctionPatterns();
        DrawCodewords(AddEccAndInterleave(dataCodewords));

        var best = 0;
        var min = int.MaxValue;
        for (var mask = 0; mask < 8; mask++)
        {
            ApplyMask(mask);
            DrawFormatBits(mask);
            var penalty = Penalty();
            if (penalty < min) { min = penalty; best = mask; }
            ApplyMask(mask); // XOR again to undo
        }
        Mask = best;
        ApplyMask(best);
        DrawFormatBits(best);
    }

    // ---- Data ----

    private static byte[] Codewords(byte[] data, int version, QrErrorLevel level, int countBits)
    {
        var capacity = DataCodewords(version, level) * 8;
        var bits = new List<bool>(capacity);
        void Append(int value, int length)
        {
            for (var i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }

        Append(0b0100, 4); // byte mode
        Append(data.Length, countBits);
        foreach (var b in data) Append(b, 8);
        Append(0, Math.Min(4, capacity - bits.Count));
        Append(0, (8 - bits.Count % 8) % 8);
        for (var pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) Append(pad, 8);

        var result = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
            if (bits[i]) result[i >> 3] |= (byte)(1 << (7 - (i & 7)));
        return result;
    }

    private byte[] AddEccAndInterleave(byte[] data)
    {
        var blocks = EccBlocks[(int)ErrorLevel][Version];
        var eccLength = EccPerBlock[(int)ErrorLevel][Version];
        var raw = RawDataModules(Version) / 8;
        var shortBlocks = blocks - raw % blocks;
        var shortLength = raw / blocks;
        var divisor = ReedSolomonDivisor(eccLength);

        var all = new List<byte[]>(blocks);
        for (int i = 0, k = 0; i < blocks; i++)
        {
            var length = shortLength - eccLength + (i < shortBlocks ? 0 : 1);
            var dat = data.AsSpan(k, length).ToArray();
            k += length;
            var ecc = ReedSolomonRemainder(dat, divisor);
            var block = new byte[shortLength + 1];
            dat.CopyTo(block, 0);
            // Short blocks get a placeholder byte so every block has the same length; it is skipped below.
            ecc.CopyTo(block, shortLength + 1 - eccLength);
            all.Add(block);
        }

        var result = new List<byte>(raw);
        for (var i = 0; i < shortLength + 1; i++)
            for (var j = 0; j < all.Count; j++)
                if (i != shortLength - eccLength || j >= shortBlocks)
                    result.Add(all[j][i]);
        return [.. result];
    }

    private static byte[] ReedSolomonDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < degree) result[j] ^= result[j + 1];
            }
            root = Multiply(root, 0x02);
        }
        return result;
    }

    private static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            var factor = b ^ result[0];
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++) result[i] ^= Multiply(divisor[i], factor);
        }
        return result;
    }

    private static byte Multiply(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    // ---- Modules ----

    private void DrawFunctionPatterns()
    {
        for (var i = 0; i < Size; i++)
        {
            SetFunction(6, i, i % 2 == 0);
            SetFunction(i, 6, i % 2 == 0);
        }

        DrawFinder(3, 3);
        DrawFinder(Size - 4, 3);
        DrawFinder(3, Size - 4);

        var positions = AlignmentPositions();
        var n = positions.Length;
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                if (!(i == 0 && j == 0 || i == 0 && j == n - 1 || i == n - 1 && j == 0))
                    DrawAlignment(positions[i], positions[j]);

        DrawFormatBits(0); // reserved now, overwritten once the mask is chosen
        DrawVersion();
    }

    private void DrawFinder(int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
            for (var dx = -4; dx <= 4; dx++)
            {
                var dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int xx = x + dx, yy = y + dy;
                if (xx >= 0 && xx < Size && yy >= 0 && yy < Size) SetFunction(xx, yy, dist != 2 && dist != 4);
            }
    }

    private void DrawAlignment(int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
                SetFunction(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }

    private int[] AlignmentPositions()
    {
        if (Version == 1) return [];
        var count = Version / 7 + 2;
        var step = (Version * 8 + count * 3 + 5) / (count * 4 - 4) * 2;
        var result = new int[count];
        result[0] = 6;
        for (int i = count - 1, pos = Size - 7; i >= 1; i--, pos -= step) result[i] = pos;
        return result;
    }

    private void DrawFormatBits(int mask)
    {
        var data = FormatLevel[(int)ErrorLevel] << 3 | mask;
        var rem = data;
        for (var i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        var bits = (data << 10 | rem) ^ 0x5412;

        for (var i = 0; i <= 5; i++) SetFunction(8, i, Bit(bits, i));
        SetFunction(8, 7, Bit(bits, 6));
        SetFunction(8, 8, Bit(bits, 7));
        SetFunction(7, 8, Bit(bits, 8));
        for (var i = 9; i < 15; i++) SetFunction(14 - i, 8, Bit(bits, i));

        for (var i = 0; i < 8; i++) SetFunction(Size - 1 - i, 8, Bit(bits, i));
        for (var i = 8; i < 15; i++) SetFunction(8, Size - 15 + i, Bit(bits, i));
        SetFunction(8, Size - 8, true); // always dark
    }

    private void DrawVersion()
    {
        if (Version < 7) return;
        var rem = Version;
        for (var i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
        var bits = Version << 12 | rem;
        for (var i = 0; i < 18; i++)
        {
            var bit = Bit(bits, i);
            int a = Size - 11 + i % 3, b = i / 3;
            SetFunction(a, b, bit);
            SetFunction(b, a, bit);
        }
    }

    private void DrawCodewords(byte[] data)
    {
        var i = 0;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (var vert = 0; vert < Size; vert++)
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? Size - 1 - vert : vert;
                    if (!_function[y, x] && i < data.Length * 8)
                    {
                        _modules[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                        i++;
                    }
                }
        }
    }

    private void ApplyMask(int mask)
    {
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                };
                if (!_function[y, x] && invert) _modules[y, x] = !_modules[y, x];
            }
    }

    // ---- Penalty (ISO 18004 section 8.8.2) ----

    private int Penalty()
    {
        var result = 0;
        for (var pass = 0; pass < 2; pass++)
            for (var a = 0; a < Size; a++)
            {
                var runColor = false;
                var run = 0;
                var history = new int[7];
                for (var b = 0; b < Size; b++)
                {
                    var color = pass == 0 ? _modules[a, b] : _modules[b, a];
                    if (color == runColor)
                    {
                        run++;
                        if (run == 5) result += 3;
                        else if (run > 5) result++;
                    }
                    else
                    {
                        AddHistory(run, history);
                        if (!runColor) result += CountFinderLike(history) * 40;
                        runColor = color;
                        run = 1;
                    }
                }
                if (runColor)
                {
                    AddHistory(run, history);
                    run = 0;
                }
                AddHistory(run + Size, history);
                result += CountFinderLike(history) * 40;
            }

        for (var y = 0; y < Size - 1; y++)
            for (var x = 0; x < Size - 1; x++)
            {
                var c = _modules[y, x];
                if (c == _modules[y, x + 1] && c == _modules[y + 1, x] && c == _modules[y + 1, x + 1]) result += 3;
            }

        var dark = 0;
        foreach (var m in _modules) if (m) dark++;
        var total = Size * Size;
        var k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
        return result + k * 10;
    }

    private void AddHistory(int run, int[] history)
    {
        if (history[0] == 0) run += Size; // the light quiet zone before the first run
        Array.Copy(history, 0, history, 1, history.Length - 1);
        history[0] = run;
    }

    private static int CountFinderLike(int[] h)
    {
        var n = h[1];
        var core = n > 0 && h[2] == n && h[3] == n * 3 && h[4] == n && h[5] == n;
        return (core && h[0] >= n * 4 && h[6] >= n ? 1 : 0) + (core && h[6] >= n * 4 && h[0] >= n ? 1 : 0);
    }

    // ---- Helpers ----

    private void SetFunction(int x, int y, bool dark)
    {
        _modules[y, x] = dark;
        _function[y, x] = true;
    }

    private static bool Bit(int value, int i) => ((value >> i) & 1) != 0;

    private static int RawDataModules(int version)
    {
        var result = (16 * version + 128) * version + 64;
        if (version >= 2)
        {
            var align = version / 7 + 2;
            result -= (25 * align - 10) * align - 55;
            if (version >= 7) result -= 36;
        }
        return result;
    }

    private static int DataCodewords(int version, QrErrorLevel level) =>
        RawDataModules(version) / 8 - EccPerBlock[(int)level][version] * EccBlocks[(int)level][version];

    private static readonly int[] FormatLevel = [1, 0, 3, 2];

    private static readonly int[][] EccPerBlock =
    [
        [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    private static readonly int[][] EccBlocks =
    [
        [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];
}

/// <summary>
/// Draws a <see cref="QrCode"/> as inline SVG: one path for the dark modules, a light background, the quiet zone
/// and an optional centred image. Both flavours emit this markup, so the Blazor and MVC codes are identical.
/// </summary>
public static class QrSvg
{
    /// <param name="code">The symbol.</param>
    /// <param name="color">Dark module colour (any CSS colour).</param>
    /// <param name="background">Background colour; "transparent" leaves it out.</param>
    /// <param name="size">Rendered width and height in pixels.</param>
    /// <param name="margin">Quiet zone in modules.</param>
    /// <param name="icon">Optional image URL drawn in the centre over a background patch. Use level H with an icon.</param>
    /// <param name="iconSize">Icon width and height in pixels.</param>
    /// <param name="label">Accessible name for the image.</param>
    public static string Render(QrCode code, string color = "#000", string background = "transparent", int size = 160,
        int margin = 0, string? icon = null, int iconSize = 40, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        var n = code.Size + margin * 2;
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.Append(inv, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 {n} {n}\" shape-rendering=\"crispEdges\" role=\"img\" aria-label=\"{Enc(label ?? "QR code")}\">");
        if (!string.Equals(background, "transparent", StringComparison.OrdinalIgnoreCase))
            sb.Append(inv, $"<rect width=\"{n}\" height=\"{n}\" fill=\"{Enc(background)}\"/>");

        sb.Append(inv, $"<path fill=\"{Enc(color)}\" d=\"");
        for (var y = 0; y < code.Size; y++)
            for (var x = 0; x < code.Size; x++)
            {
                if (!code[x, y]) continue;
                // Merge horizontal runs into one rectangle to keep the path short.
                var start = x;
                while (x + 1 < code.Size && code[x + 1, y]) x++;
                sb.Append(inv, $"M{start + margin} {y + margin}h{x - start + 1}v1h-{x - start + 1}z");
            }
        sb.Append("\"/>");

        if (!string.IsNullOrEmpty(icon))
        {
            var box = (double)iconSize / size * n;
            var at = (n - box) / 2;
            var pad = box * 0.1;
            var patch = string.Equals(background, "transparent", StringComparison.OrdinalIgnoreCase) ? "#fff" : background;
            sb.Append(inv, $"<rect x=\"{F(at - pad)}\" y=\"{F(at - pad)}\" width=\"{F(box + pad * 2)}\" height=\"{F(box + pad * 2)}\" fill=\"{Enc(patch)}\"/>");
            sb.Append(inv, $"<image href=\"{Enc(icon)}\" x=\"{F(at)}\" y=\"{F(at)}\" width=\"{F(box)}\" height=\"{F(box)}\" preserveAspectRatio=\"xMidYMid meet\"/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string F(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);

    private static string Enc(string value) => WebUtility.HtmlEncode(value);
}
