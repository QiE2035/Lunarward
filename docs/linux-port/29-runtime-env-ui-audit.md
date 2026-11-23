# 29 · 运行环境页面 UI 审查

> 审查日期：2026-11-23　范围：`src/Lunarward/Features/Setting/RuntimeEnvironmentSetting.*`（XAML + 4 个 partial）
> 依据：[23 运行环境设计](23-runtime-env-design.md) §7/§13、D80/D82/D84/D85、AGENTS.md 硬规则（禁止硬编码颜色、危险入口不进发行版）
> 方法：真机逐页走查（`cu_screenshot target=area`，1200×680 窗口）+ 读 XAML/code-behind 对照设计；只读审查，修与不修由用户拍板。
> 结论：共 21 条，**2026-11-23 用户确认「全部处理」，全部已修（D108）**。

| 分类 | 条数 | 已修 |
| --- | --- | --- |
| 术语与 i18n | 7 | 7 |
| 布局与视觉 | 8 | 8 |
| 交互与可达性 | 6 | 6 |

## 1 · 术语与 i18n

1. **「配置」「方案」混用**（描述「每个配置」、按钮「新建配置」、页签「方案」、按钮「编辑方案」）。
   🛠 D108：统一为「方案」；`Lunarward_RuntimeEnv_New`→「新建方案」，`NoProfilesHint`/`NoTemplates`/`TemplatesNote` 同步改词。
2. **「图形组件」「组件」「图形层」三种叫法**（chip「图形组件 6」、页签「组件」、段标题「图形层（DXVK / VKD3D）」）。
   🛠 D108：统一为「图形层」；页签 `Components_2`→「图形层」、`ChipGraphics`→「图形层 {0}」、`GraphicsFollowPrefix`→「图形层：沿用前缀里已有的」。
3. **同一编辑动作四种叫法**（运行器段「更改」、图形层段「编辑方案」、前缀段「打开目录」、增强段「配置」）。
   🛠 D108：运行器「更换」、图形层「编辑图形层」、增强「设置」；顶部「编辑方案」保留（它是整体入口）。
4. **「配置文本」与设计 §7.4「文本视图（只读）」不一致**。
   🛠 D108：`TextView`→「文本视图」，窗口标题 `ConfigTextTitle`→「{0} · 文本视图（只读）」。
5. **88 处用户可见中文硬编码**（`RuntimeEnvironmentSetting*.cs`，切语言不生效）。
   🛠 D108：全部换成 `Lunarward_RuntimeEnv_*` 键（新增约 110 个），四个文件 `grep [一-鿿]` 归零；XAML 本无硬编码中文（仅注释）。
6. **运行器徽标把裸 `bin` 上屏**（「可用 · bin · 1 个方案在用」）。
   🛠 D108：新增 `DescribeLayout`，`bin`→「bin 目录」、`system`→「系统目录」、`prefix`→「wine 前缀」；编辑器下拉同源。
7. **体积小数位不一致**（chip「370.7 MB」vs 卡片「370.71 MB」）。
   🛠 D108：`FormatSize` 统一委托 `InstallPlanner.FormatSize`（两位小数）。

## 2 · 布局与视觉

8. **方案清单左侧 Border 不撑满高度**（只包住卡片，下方大片空白）。
   🛠 D108：清单容器改为星号行 + `VerticalAlignment="Stretch"`，空态文案居中。
9. **前缀卡标题「environment · hk4e_cn」**（Mode + GameKey 直接拼接，既不是方案名也不是前缀目录名）。
   🛠 D108：标题改为「{归属方案} · {目录名}」，0 个方案时「未使用」/「外部前缀」。
10. **前缀占用条写死 320 px 且无标签**。
    🛠 D108：两列星号 Grid 按比例布局（`ratio = size / max`），右侧显示「占最大前缀 N%」。
11. **详情段动作按钮与顶部按钮重复、文案不统一**。
    🛠 D108：见第 3 条；图形层段按钮不再叫「编辑方案」。
12. **编辑器保存/取消在滚动区最底部，680 高窗口要滚到底才能保存**。
    🛠 D108：编辑器改成 `Grid`（内容 `*` + 底栏 `Auto`），保存/取消固定底部。
13. **增强面板「命令预览」注释说固定，实际随表单滚走**。
    🛠 D108：增强面板同样拆成三行 Grid，预览与保存/还原固定在下方。
14. **长路径按字符中截，目录名被切碎**（`/home/qie2035/.local/shar….ward/components/graphics/dx…`），无 tooltip。
    🛠 D108：新增 `ShortenPath`（按 `/` 分段，保留首段 + 末两段）并给路径/版本行加 `ToolTipService` 全文；前缀卡路径可选中复制。
15. **硬编码颜色 hex**（`DescribeHealth` 返回 `#C42B1C`/`#9D5D00`/`#0F7B0F`，`Profiles.cs` 用 `BrushOf("…")` 解析）。
    🛠 D108：改为主题 token（`Critical`/`Caution`/`Success`）+ `SystemFillColor*Brush`，`BrushOf` 不再解析 hex。

## 3 · 交互与可达性

16. **「⋯」菜单无分隔线、删除无危险色、「打开调试 Shell」出现在常规 UI**。
    🛠 D108：菜单项分组 + 分隔线；删除项 `Foreground=SystemFillColorCriticalBrush`；调试 Shell 仅在 `DevSwitches.IsEnabled`（Release 不出现，硬规则 6）。
17. **编辑器没有脏检查**：切页签/返回/切方案会静默丢弃草稿。
    🛠 D108：`_editorSnapshot` + `ConfirmLeaveEditorAsync()`，切页签与返回都会先弹「放弃未保存的修改？」。
18. **页签无选中状态暴露给读屏**。
    🛠 D108：`ShowTab` 里对四个页签 `AutomationProperties.SetItemStatus(…, "Selected")`。
19. **「⋯」按钮只有 ToolTip、无自动化名**。
    🛠 D108：`AutomationProperties.SetName(moreButton, Lang…More)`。
20. **无快捷键**。
    🛠 D108：Ctrl+N 新建方案、Ctrl+S 保存编辑、Delete 删除当前方案（详情页时）。
21. **`--subpage RuntimeEnvironmentSetting` 无效**（落到启动器页）。
    🛠 D108：`MainView_Loaded` 在只有 `--subpage` 时也先导航到 `SettingPage`；真机验证可直达。

## 4 · 验证

- `dotnet build Starward.slnx -c Debug`：0 错误；`dotnet test`：270 通过。
- `bash scripts/test/lint-ui-thread.sh`：全部通过。
- 真机（1200×680）：运行环境页四页签、chip、前缀卡（标题「默认（opt）· .wine」、满宽占用条 + 「占最大前缀 100%」）、编辑器固定底栏与脏检查弹窗均按预期渲染。

## 5 · 文案与本地化

- 本页新增的 `Lunarward_RuntimeEnv_*` 键（331 个）在**中性 `Lang.resx`（英文）与 `Lang.zh-CN.resx`（简体）**里是完整的一套；`zh-TW` / `zh-HK` 用 OpenCC（`s2twp` / `s2hk`）从简体转换补齐了另外 313 个键，繁体界面不再中英混杂。
- 德/西/意/日/韩/俄/泰/越等 9 种语言沿用 D73 的既有做法：只维护已人工翻译的子集，缺键由 .NET 资源回落机制使用中性英文。**不**把英文机械复制进这些文件冒充翻译——那会让后续译者分不清「已译」与「待译」。补翻译时按 `Lunarward_RuntimeEnv_*` 键名比对即可（简体与英文两套都是完整的）。
- 「文本视图」的复制/导出在 D73 实现时就已具备（`Button_TextView_Click`：Primary=复制 JSON 到剪贴板、Secondary=另存为 `.json`）；本次只统一了命名（「配置文本」→「文本视图」）与只读标题，无功能缺口。
- 真机复测补记（同日，D108 补丁）：
  - 脏检查弹窗点「放弃修改」后，右栏原本仍停在编辑器（`Tab_Click` 只调 `ShowTab`，而 `ShowTab` 只管页签可见性）——改为确认放弃后先 `CloseEditor()` 再切页签。
  - 快捷键原先挂在本页 `KeyDown` 上，而页面本身不可聚焦：导航进来后不点任何控件时 Ctrl+N/Ctrl+S 收不到。改为挂在窗口根元素（`XamlRoot.Content`）并在进入页面时把焦点放到当前页签按钮。
  
