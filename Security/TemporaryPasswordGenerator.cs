using System.Security.Cryptography;

namespace BitacoraEvidencias.Web.Security;

public static class TemporaryPasswordGenerator
{
    private static readonly char[] Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] Lower = "abcdefghijkmnopqrstuvwxyz".ToCharArray();
    private static readonly char[] Digits = "23456789".ToCharArray();
    private static readonly char[] Symbols = "!@$%*?-_".ToCharArray();

    public static string Generate(int length = 12)
    {
        var size = Math.Max(10, length);
        var result = new List<char>(size)
        {
            GetRandom(Upper),
            GetRandom(Lower),
            GetRandom(Digits),
            GetRandom(Symbols)
        };

        var all = Upper.Concat(Lower).Concat(Digits).Concat(Symbols).ToArray();
        while (result.Count < size)
        {
            result.Add(GetRandom(all));
        }

        Shuffle(result);
        return new string(result.ToArray());
    }

    private static char GetRandom(IReadOnlyList<char> source)
    {
        return source[RandomNumberGenerator.GetInt32(source.Count)];
    }

    private static void Shuffle(IList<char> values)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
