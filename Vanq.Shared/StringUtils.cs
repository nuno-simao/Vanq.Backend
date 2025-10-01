namespace Vanq.Shared;

public static class StringUtils
{
    public static bool IsValidKebabCase(this string key)
    {
        // kebab-case: lowercase letters, numbers, and hyphens only
        // Must start with a letter, cannot have consecutive hyphens
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (!char.IsLetter(key[0])) return false;
        
        for (int i = 0; i < key.Length; i++)
        {
            char c = key[i];
            if (!char.IsLower(c) && !char.IsDigit(c) && c != '-')
                return false;
            
            // No consecutive hyphens
            if (c == '-' && i + 1 < key.Length && key[i + 1] == '-')
                return false;
        }

        // Cannot end with hyphen
        return key[^1] != '-';
    }
}