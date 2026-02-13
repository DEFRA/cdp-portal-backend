using System.Text.RegularExpressions;

namespace Defra.Cdp.Backend.Api.Utils;

public static partial class SemVer
{

    [GeneratedRegex(@"^v?\d+\.\d+\.\d+$", RegexOptions.Compiled)]
    private static partial Regex SemVerRegex();

    public static bool IsSemVer(string s)
    {
        return SemVerRegex().IsMatch(s);
    }

    // Turns a semver string 1.2.3 into a unsigned 64 bit long
    // in which bits [0-16] = patch, [16-32] = min & [32-48] = maj
    // This should make sorting/comp easier since strings have issues with 10 being > 9 etc
    // Also plays nice in mongo allowing for semver range searches without regex.
    public static long SemVerAsLong(string input)
    {
        long result = 0;
        var part = 0;
        var mut = 1;
        var shift = 0;
        var s = input.Reverse().ToArray();

        foreach (var t in s)
            switch (t)
            {
                case >= '0' and <= '9':
                    part += (t - 48) * mut;
                    mut *= 10;
                    break;
                case '.':
                    result |= (long)part << shift;
                    part = 0;
                    shift += 16;
                    mut = 1;
                    break;
                case 'v': // skip v prefixes
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(input), input, "Is not valid semver");
            }

        result |= (long)part << shift;

        return result;
    }
}
