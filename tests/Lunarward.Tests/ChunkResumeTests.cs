using Starward.Features.GameInstall.Sophon;
using System;
using System.Collections.Generic;
using Xunit;

namespace Lunarward.Tests;


public class ChunkResumeTests
{
    private static readonly long[] Sizes = { 100, 200, 300, 400 };


    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]      // 第一块没写完
    [InlineData(100, 1)]     // 正好一块
    [InlineData(299, 1)]     // 第二块写了一部分
    [InlineData(300, 2)]
    [InlineData(1000, 4)]    // 全部写完
    [InlineData(1001, 4)]    // 比预期长也不会多算
    public void CountCompletedChunks_ReturnsBoundary(long length, int expected)
    {
        Assert.Equal(expected, ChunkResume.CountCompletedChunks(length, Sizes));
    }


    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    [InlineData(2, 300)]
    [InlineData(4, 1000)]
    [InlineData(9, 1000)]
    public void LengthOfChunks_ClampsToAvailable(int count, long expected)
    {
        Assert.Equal(expected, ChunkResume.LengthOfChunks(Sizes, count));
    }


    [Fact]
    public void CountCompletedChunks_EmptyOrNegativeSizeIsSafe()
    {
        Assert.Equal(0, ChunkResume.CountCompletedChunks(100, Array.Empty<long>()));
        // 负值（坏清单）直接放弃续传，而不是算出一个离谱的长度。
        Assert.Equal(0, ChunkResume.CountCompletedChunks(100, new List<long> { 50, -1, 50 }));
    }
}
