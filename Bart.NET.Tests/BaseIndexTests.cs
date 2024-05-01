namespace Bart.NET.Tests;

public class BaseIndexTests
{
    [Fact]
    public void TestInverseIndex()
    {
        for (int i = 0; i < MaxNodeChildren; ++i)
        {
            for (int bits = 0; bits <= StrideLength; bits++)
            {
                byte octet = (byte)(i & (0xFF << (StrideLength - bits)));
                uint idx = PrefixToBaseIndex(octet, bits);
                (byte octet2, int len2) = BaseIndexToPrefix(idx);
                Assert.Equal(octet2, octet);
                Assert.Equal(len2, bits);
            }
        }
    }

    [Fact]
    public void TestFringeIndex()
    {
        for (int i = 0; i < MaxNodeChildren; ++i)
        {
            uint got = OctetToBaseIndex((byte)i);
            uint want = PrefixToBaseIndex((byte)i, 8);
            Assert.Equal(want, got);
        }
    }
}
