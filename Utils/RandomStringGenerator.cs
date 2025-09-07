using System.Text;

namespace Tester.Utils;

public static class RandomStringGenerator
{
    private static readonly Random _random = new();
    
    // Conjuntos de caracteres predefinidos
    private static readonly string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string Numbers = "0123456789";
    private static readonly string LettersAndNumbers = Letters + Numbers;
    private static readonly string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    

    public static string GenerateLetters(int length)
    {
        return GenerateFromCharset(Letters, length);
    }
    

    public static string GenerateNumbers(int length)
    {
        return GenerateFromCharset(Numbers, length);
    }
    

    public static string GenerateAlphanumeric(int length)
    {
        return GenerateFromCharset(LettersAndNumbers, length);
    }
    

    public static string GenerateComplex(int length)
    {
        return GenerateFromCharset(LettersAndNumbers + SpecialChars, length);
    }
    
    public static string GenerateCode(int length)
    {
        return GenerateFromCharset(LettersAndNumbers, length);
    }
    
    private static string GenerateFromCharset(string charset, int length)
    {
        var result = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            result.Append(charset[_random.Next(charset.Length)]);
        }
        return result.ToString();
    }
}