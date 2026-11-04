// Lunarward：Uno 的 x:Bind 解析器不接受负数实参（`f(x, -1)` → UXAML0001 "Invalid identifier character -"）。
// 这里把哨兵值 -1 提成常量，XAML 里以 local:ElementWeaknessConstants.None 传入，语义完全不变。
namespace Starward.Features.GameRecord.ZZZ;

public static class ElementWeaknessConstants
{
    /// <summary>表示「无该元素弱点」的哨兵值，与上游 -1 一致。</summary>
    public const int None = -1;
}
