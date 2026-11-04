# 10 · 上游 XAML 兼容性体检（静态统计 + 实测）

> 目的：在动 `src/` 之前，先量化「82 个 XAML 到底有多少 Uno 不吃」。静态扫描全仓，再用 PoC 逐项实测高频写法。
> 产出对照：`poc/GATES.md` G0-9；截图 `poc/screenshots/{9-compat,10-compat-2,11-compat-3,12-dialog}.png`。

## 1. 静态统计（`src/Starward` 下 82 个 .xaml）

| 构造 | 出现文件数 | Uno 现状 | 结论 |
|---|---|---|---|
| `x:Bind` | **72 / 82** | 支持（含函数调用、Converter、TwoWay、`x:Load`） | **实测通过** |
| `x:Bind` 函数调用 | 20 | 支持 | 实测通过（`{x:Bind Describe(Count)}`） |
| `x:DataType` | 35 | 支持 | 随 `x:Bind` 一起编译通过 |
| Converter 绑定 | 40 | 支持 | 实测通过 |
| TwoWay 绑定 | 27 | 支持 | 编译通过（交互未逐项验证） |
| `FontIcon` | **55** | 控件存在，但**忽略 `FontFamily`** | **需替代控件**（D26） |
| `AcrylicBrush` | 39 | 可渲染（半透明着色） | **实测通过**，但与 Windows 亚克力有观感差 |
| `ThemeShadow` | 23 | 支持（渲染阴影） | 实测通过，强度偏弱 |
| `x:Load` | 8 | 支持（**要求元素带 `x:Name`**） | 实测通过 |
| `ItemsRepeater` | 14 | 支持 | **实测通过** |
| `ContentDialog` | 10 | 支持（含 `XamlRoot`） | **实测通过**（深色主题正常） |
| `WebView2` | 7 | 渲染/导航可用，cookie/JS 不可用 | 见 `G0-3b` 与 D29 |
| `NavigationView` | 5 | 支持 | PoC 主界面已用 |
| `InfoBar` | 3 | 支持 | 实测通过 |
| `PipsPager` | 2 | 支持 | **实测通过**（5 个 pip） |
| `AnimatedIcon` | 3 | 存在但依赖 `IAnimatedVisualSource` | **待 M1 验证**（上游用于按钮/复选框状态动画） |
| `TeachingTip` | 1 | 可构造（未展开） | 待交互验证 |
| `MicaBackdrop` / `SystemBackdrop` | 各 1 | Windows 专有 | **预计不可用**，降级为无背景 |
| `ThemeDictionaries` | 1 | 支持 | 未实测 |
| 自定义命名空间控件（`sf:`51 / `sc:`31 / `sh:`11 / `cwc:`10 …） | 多 | 就是普通 UserControl/CustomControl | 取决于其代码后置 |

**静态结论**：XAML 方言本身不是瓶颈。72 个文件用 `x:Bind`，这是 Uno 支持最好的绑定方式；真正会带来返工的只有三类——`FontIcon` 字体（55 个文件，D26 已定替代控件）、`WebView2` 取值（7 个文件，D29 已定绕行）、以及 `MicaBackdrop`/`AnimatedIcon` 这类边角。

## 2. 实测方法

`poc/uno-smoke/LunarwardPoc/CompatPage.xaml` 一页塞进上游最高频的 11 种写法，构建 + 运行 + 截图：

```bash
GDK_BACKEND=x11 WEBKIT_DISABLE_DMABUF_RENDERER=1 DISPLAY=:0 ./LunarwardPoc --page=compat
```

编译期唯一报错是 `x:Load` 缺 `x:Name`（`UXAML0001`），加名即过。运行时无异常。

| # | 构造 | 结果 |
|---|---|---|
| ① | `{x:Bind Describe(Count), Mode=OneWay}` | 渲染 `Count = 3（x:Bind 函数返回值）` |
| ② | `{x:Bind IsReady, Converter={StaticResource BoolToText}}` | 渲染 `转换器：true → 是` |
| ③ | TwoWay + `UpdateSourceTrigger=PropertyChanged` | 编译通过，双向绑定已接 |
| ④ | `x:Load` 条件加载 | 绿色块按 `IsReady=true` 显示 |
| ⑤ | `AcrylicBrush`（TintOpacity 0.6） | 半透明灰块正常 |
| ⑥ | `ThemeShadow` + `Translation="0,0,32"` | 阴影存在但很淡 |
| ⑦ | `ItemsRepeater` + `StackLayout` | 3 条数据正确渲染 |
| ⑧ | `PipsPager NumberOfPages=5` | 5 个圆点正常 |
| ⑨ | `InfoBar` | 深色下配色正确 |
| ⑩ | `ContentDialog`（代码创建 + `XamlRoot`） | 弹层正常，与上游 `DeleteGachaLogDialog` 形态一致 |
| ⑪ | `TeachingTip` | 可构造 |

## 3. 对 M1 的直接输入

1. **不要为 XAML 方言做改造**：`x:Bind`/Converter/TwoWay/`x:Load`/`ItemsRepeater`/`ContentDialog` 都是原生支持，直接搬。
2. `FontIcon` 替换脚本要在**导入 XAML 之后立刻跑**，否则每个页面都要返工一次。
3. `AcrylicBrush`/`ThemeShadow` 保留原写法，把「观感差异」归入 D3 的软差异；M3 再调 tint/阴影参数。
4. 需要 M1-0 在真实页面里再确认的：`AnimatedIcon`（3 处）、`MicaBackdrop`（1 处）、`TeachingTip`（1 处）、`ThemeDictionaries`（1 处）、`cwc:` 系列控件模板（10 处）。
5. 编译性能：`x:Bind` 在 Uno 下是编译期代码生成，82 个页面 + 55 个 `FontIcon` 替换后，首次全量编译时间要在 M1-0 记一次基线（当前 PoC 6 页约 3–8 秒）。
