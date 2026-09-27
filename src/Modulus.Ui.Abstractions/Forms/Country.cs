using System.Text;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>A country for phone and address fields: ISO 3166 alpha-2 code, English name and calling code.</summary>
public sealed record Country(string Code, string Name, string DialCode)
{
    /// <summary>Countries by name. Pass your own list to a field to limit or reorder it.</summary>
    public static IReadOnlyList<Country> All { get; } =
    [
        new("AF", "Afghanistan", "93"), new("AL", "Albania", "355"), new("DZ", "Algeria", "213"), new("AD", "Andorra", "376"),
        new("AO", "Angola", "244"), new("AR", "Argentina", "54"), new("AM", "Armenia", "374"), new("AU", "Australia", "61"),
        new("AT", "Austria", "43"), new("AZ", "Azerbaijan", "994"), new("BH", "Bahrain", "973"), new("BD", "Bangladesh", "880"),
        new("BY", "Belarus", "375"), new("BE", "Belgium", "32"), new("BJ", "Benin", "229"), new("BT", "Bhutan", "975"),
        new("BO", "Bolivia", "591"), new("BA", "Bosnia and Herzegovina", "387"), new("BW", "Botswana", "267"), new("BR", "Brazil", "55"),
        new("BN", "Brunei", "673"), new("BG", "Bulgaria", "359"), new("KH", "Cambodia", "855"), new("CM", "Cameroon", "237"),
        new("CA", "Canada", "1"), new("CL", "Chile", "56"), new("CN", "China", "86"), new("CO", "Colombia", "57"),
        new("CR", "Costa Rica", "506"), new("CI", "Côte d'Ivoire", "225"), new("HR", "Croatia", "385"), new("CU", "Cuba", "53"), new("CY", "Cyprus", "357"),
        new("CZ", "Czechia", "420"), new("DK", "Denmark", "45"), new("DO", "Dominican Republic", "1"), new("EC", "Ecuador", "593"),
        new("EG", "Egypt", "20"), new("SV", "El Salvador", "503"), new("EE", "Estonia", "372"), new("ET", "Ethiopia", "251"),
        new("FI", "Finland", "358"), new("FR", "France", "33"), new("GE", "Georgia", "995"), new("DE", "Germany", "49"),
        new("GH", "Ghana", "233"), new("GR", "Greece", "30"), new("GT", "Guatemala", "502"), new("HN", "Honduras", "504"),
        new("HK", "Hong Kong", "852"), new("HU", "Hungary", "36"), new("IS", "Iceland", "354"), new("IN", "India", "91"),
        new("ID", "Indonesia", "62"), new("IR", "Iran", "98"), new("IQ", "Iraq", "964"), new("IE", "Ireland", "353"),
        new("IL", "Israel", "972"), new("IT", "Italy", "39"), new("JM", "Jamaica", "1"),
        new("JP", "Japan", "81"), new("JO", "Jordan", "962"), new("KZ", "Kazakhstan", "7"), new("KE", "Kenya", "254"),
        new("KW", "Kuwait", "965"), new("KG", "Kyrgyzstan", "996"), new("LA", "Laos", "856"), new("LV", "Latvia", "371"),
        new("LB", "Lebanon", "961"), new("LY", "Libya", "218"), new("LI", "Liechtenstein", "423"), new("LT", "Lithuania", "370"),
        new("LU", "Luxembourg", "352"), new("MO", "Macao", "853"), new("MG", "Madagascar", "261"), new("MY", "Malaysia", "60"),
        new("MV", "Maldives", "960"), new("MT", "Malta", "356"), new("MU", "Mauritius", "230"), new("MX", "Mexico", "52"),
        new("MD", "Moldova", "373"), new("MC", "Monaco", "377"), new("MN", "Mongolia", "976"), new("ME", "Montenegro", "382"),
        new("MA", "Morocco", "212"), new("MZ", "Mozambique", "258"), new("MM", "Myanmar", "95"), new("NA", "Namibia", "264"),
        new("NP", "Nepal", "977"), new("NL", "Netherlands", "31"), new("NZ", "New Zealand", "64"), new("NI", "Nicaragua", "505"),
        new("NG", "Nigeria", "234"), new("MK", "North Macedonia", "389"), new("NO", "Norway", "47"), new("OM", "Oman", "968"),
        new("PK", "Pakistan", "92"), new("PA", "Panama", "507"), new("PY", "Paraguay", "595"), new("PE", "Peru", "51"),
        new("PH", "Philippines", "63"), new("PL", "Poland", "48"), new("PT", "Portugal", "351"), new("PR", "Puerto Rico", "1"),
        new("QA", "Qatar", "974"), new("RO", "Romania", "40"), new("RU", "Russia", "7"), new("RW", "Rwanda", "250"),
        new("SA", "Saudi Arabia", "966"), new("SN", "Senegal", "221"), new("RS", "Serbia", "381"), new("SG", "Singapore", "65"),
        new("SK", "Slovakia", "421"), new("SI", "Slovenia", "386"), new("ZA", "South Africa", "27"), new("KR", "South Korea", "82"),
        new("ES", "Spain", "34"), new("LK", "Sri Lanka", "94"), new("SE", "Sweden", "46"), new("CH", "Switzerland", "41"),
        new("TW", "Taiwan", "886"), new("TJ", "Tajikistan", "992"), new("TZ", "Tanzania", "255"), new("TH", "Thailand", "66"),
        new("TT", "Trinidad and Tobago", "1"), new("TN", "Tunisia", "216"), new("TR", "Türkiye", "90"), new("TM", "Turkmenistan", "993"),
        new("UG", "Uganda", "256"), new("UA", "Ukraine", "380"), new("AE", "United Arab Emirates", "971"), new("GB", "United Kingdom", "44"),
        new("US", "United States", "1"), new("UY", "Uruguay", "598"), new("UZ", "Uzbekistan", "998"), new("VE", "Venezuela", "58"),
        new("VN", "Vietnam", "84"), new("YE", "Yemen", "967"), new("ZM", "Zambia", "260"), new("ZW", "Zimbabwe", "263"),
    ];

    /// <summary>The country with this ISO code (case-insensitive) in <paramref name="list"/> (default <see cref="All"/>).</summary>
    public static Country? Find(string? code, IEnumerable<Country>? list = null) =>
        string.IsNullOrWhiteSpace(code) ? null
            : (list ?? All).FirstOrDefault(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// International phone numbers as one string, "+44 20 7946 0958": the calling code, a space, then the national
/// number as typed. Shared by AppPhoneInput and &lt;app-phone-input&gt; (mirrored by <c>mPhone</c> in modulus-widgets.js).
/// </summary>
public static class PhoneNumber
{
    /// <summary>
    /// Splits a stored number into its country and national part. Calling codes shared by several countries
    /// (+1, +7) resolve to <paramref name="preferred"/> when it has that code, otherwise to the first in the list.
    /// Text without a leading "+" is a national number of the preferred country.
    /// </summary>
    public static (Country? Country, string National) Split(string? value, IEnumerable<Country>? countries = null, string? preferred = null)
    {
        var list = (countries ?? Country.All).ToList();
        var fallback = Country.Find(preferred, list) ?? list.FirstOrDefault();
        var s = (value ?? "").Trim();
        if (!s.StartsWith('+')) return (fallback, s);

        var digits = Digits(s);
        for (var len = Math.Min(4, digits.Length); len >= 1; len--)
        {
            var code = digits[..len];
            var matches = list.Where(c => c.DialCode == code).ToList();
            if (matches.Count == 0) continue;
            var country = matches.FirstOrDefault(c => c.Code == fallback?.Code) ?? matches[0];
            return (country, NationalPart(s, len));
        }
        return (fallback, s[1..].Trim());
    }

    /// <summary>"+{code} {national}", or "" when the national part has no digits.</summary>
    public static string Join(Country? country, string? national)
    {
        var n = Clean(national);
        if (Digits(n).Length == 0) return "";
        return country is null ? n : $"+{country.DialCode} {n}";
    }

    /// <summary>E.164 shape: a "+", a known length of 8 to 15 digits in all.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith('+')) return false;
        if (value.Any(c => !(char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')' or '.'))) return false;
        var d = Digits(value).Length;
        return d is >= 8 and <= 15;
    }

    /// <summary>Only the digits: "+1 (202) 555-0111" → "12025550111".</summary>
    public static string Digits(string? value) => string.Concat((value ?? "").Where(char.IsAsciiDigit));

    /// <summary>Keeps digits and the separators people type (space, hyphen, dot, parentheses), collapsing spaces.</summary>
    public static string Clean(string? national)
    {
        var sb = new StringBuilder();
        foreach (var c in (national ?? "").Trim())
        {
            if (char.IsAsciiDigit(c) || c is '-' or '.' or '(' or ')') sb.Append(c);
            else if (char.IsWhiteSpace(c) && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    // The text after the first `codeDigits` digits of the calling code.
    private static string NationalPart(string s, int codeDigits)
    {
        var seen = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (!char.IsAsciiDigit(s[i])) continue;
            if (++seen == codeDigits) return Clean(s[(i + 1)..]);
        }
        return "";
    }
}
