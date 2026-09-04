using System.Text.RegularExpressions;

namespace Defra.Cdp.Backend.Api.Utils;

public static partial class SemVer
{
    [GeneratedRegex(@"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+(?<build>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$", RegexOptions.Compiled)]
    private static partial Regex SemVerRegex();

    public static bool IsSemVer(string s)
    {
        return SemVerRegex().IsMatch(s);
    }

    // Turns a semver string into an unsigned 64 bit long: bits [0-16] = patch, [16-32] = min, [32-48] = maj.
    // Makes sorting/comparison easier and plays nice with mongo range searches without regex.
    // Best-effort: -prerelease/+build suffixes are ignored, so "1.2.3" and "1.2.3-rc.1" pack the same
    // (keeps existing persisted values backward-compatible). Same-core ties are broken by Created
    // (descending) at the call site, since a release is always tagged after its own candidates.
    public static long SemVerAsLong(string input)
    {
        var match = SemVerRegex().Match(input);
        if (!match.Success)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input, "Is not valid semver");
        }

        var major = ParsePart(match, "major");
        var minor = ParsePart(match, "minor");
        var patch = ParsePart(match, "patch");

        return patch | (minor << 16) | (major << 32);
    }

    private static long ParsePart(Match match, string groupName)
    {
        return long.Parse(match.Groups[groupName].Value);
    }
}
