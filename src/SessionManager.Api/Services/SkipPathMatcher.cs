namespace SessionManager.Api.Services;

public static class SkipPathMatcher
{
    public static bool ShouldSkipPath(string requestPath, string? skipPaths)
    {
        if (string.IsNullOrEmpty(skipPaths) || string.IsNullOrEmpty(requestPath))
        {
            return false;
        }

        // Split comma-separated patterns and check each one
        var patterns = skipPaths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pattern in patterns)
        {
            if (RegexMatches(requestPath, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RegexMatches(string input, string pattern)
    {
        try
        {
            // The pattern comes from oauth2-proxy format: ^/nodes(/.*)?$
            // We need to convert it to a valid .NET regex pattern
            var netPattern = ConvertPattern(pattern);
            return System.Text.RegularExpressions.Regex.IsMatch(input, netPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            // If regex is invalid, treat as no match
            return false;
        }
    }

    private static string ConvertPattern(string pattern)
    {
        // oauth2-proxy uses Go regex syntax, .NET uses slightly different syntax
        // For basic patterns like ^/path(/.*)?$ they should be compatible
        // Just ensure we have proper anchoring

        // If pattern doesn't start with ^, add it to match from start
        if (!pattern.StartsWith('^'))
        {
            pattern = "^" + pattern;
        }

        // If pattern doesn't end with $, we should allow trailing content
        // (oauth2-proxy patterns are usually anchored with $)

        return pattern;
    }
}
