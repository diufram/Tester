using System.Security.Cryptography;

namespace Tester.Utils;

public static class RandomUtil
{
    public static int NextInt(int inclusiveMin, int exclusiveMax)
        => RandomNumberGenerator.GetInt32(inclusiveMin, exclusiveMax);
}
