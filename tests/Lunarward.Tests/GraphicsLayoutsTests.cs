using Starward.Features.Runtime;
using System;
using System.IO;
using Xunit;

namespace Lunarward.Tests;


/// <summary>R59：导入校验与组件发现必须用同一套「图形层根」判定。</summary>
public class GraphicsLayoutsTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-gfx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }


    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' });
    }


    [Fact]
    public void FindRoot_SkipsSiblingReadme_AndFindsNestedX64Marker()
    {
        string root = NewTempDir();
        try
        {
            // 归档顶层不是唯一目录（常见：README/LICENSE 混在 dxvk-3.1.1/ 旁边）
            File.WriteAllText(Path.Combine(root, "README.md"), "readme");
            File.WriteAllText(Path.Combine(root, "LICENSE"), "license");
            Touch(Path.Combine(root, "dxvk-3.1.1", "x64", "d3d11.dll"));

            string? found = GraphicsLayouts.FindRoot(root);

            Assert.Equal(Path.Combine(root, "dxvk-3.1.1"), found);
            Assert.True(GraphicsLayouts.IsRoot(found!));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public void FindRoot_AcceptsTiledLayout()
    {
        string root = NewTempDir();
        try
        {
            Touch(Path.Combine(root, "d3d11.dll"));

            Assert.Equal(root, GraphicsLayouts.FindRoot(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public void FindRoot_FallsBackToVkd3dMarker()
    {
        string root = NewTempDir();
        try
        {
            Touch(Path.Combine(root, "x64", "d3d12.dll"));

            Assert.Equal(root, GraphicsLayouts.FindRoot(root));
            Assert.True(GraphicsLayouts.IsRoot(root, vkd3d: true));
            Assert.False(GraphicsLayouts.IsRoot(root, vkd3d: false));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public void FindRoot_ReturnsNull_WhenNoMarker()
    {
        string root = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "README.md"), "readme");

            Assert.Null(GraphicsLayouts.FindRoot(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
