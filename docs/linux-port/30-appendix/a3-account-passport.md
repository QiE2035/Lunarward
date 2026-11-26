# C 组：账号与 Passport（4 文件）

审计方式：两侧源文件逐行对读；diff 行数取自 `/tmp/lw-audit2/diffs/Features__*.u0`（扁平命名，非 `<相对路径>.u0`）；所有移植侧行号用 `grep -n` / `sed -n` 复核，不按 diff 头部推算。
编译状态判定依据：`src/Lunarward/Lunarward.csproj` 的 `<Compile Remove>` 全表在 `:33-36`、`:98-141`，四个文件均不在表内。

## GameAccountService.cs
- 变更行数：90（删除 12 / 新增 64），14 个 hunk
- 编译状态：参与编译（不在 `<Compile Remove>` 表内）；无 `GameAccountService.Linux.cs` 覆盖，同目录只有 `WineRegistry.cs` / `WineRegistryFiles.cs` / `RegistryFormat.cs` 等辅助类

### 差异清单

- **[移植 第 1-11 行 · 文件头 / using 区]** 平台缺失｜功能损失：否｜建议：保留｜上游：`using Microsoft.Win32;`（上游 L3）+ 仅 `System.Text` 等｜移植：删除 `Microsoft.Win32`，新增 `System.Threading`(L10)、`System.Threading.Tasks`(L11)｜理由：Linux 无 Win32 注册表 API，改用自研 `WineRegistry`；新增的 using 是为 async/CTS 签名服务。
- **[移植 第 29-30 行 · `GetGameAccountFromRegistry`]** 移植新增｜功能损失：否｜建议：保留｜上游：L28 起直接是同步方法体（读注册表 3-4 次 `GetValue`）｜移植：一行表达式体 `=> GetGameAccountFromRegistryAsync(biz).GetAwaiter().GetResult();`｜理由：保留上游同步签名；实测全仓无调用点（移植/上游客都只经 `GameAccountSwitcher`，且那边已用 Async 版），属签名兼容而非死代码（上游同名 API 也是公开方法）。
- **[移植 第 34-38 行 · `GetGameAccountFromRegistryAsync` 声明]** 移植新增｜功能损失：否｜建议：保留｜上游：无异步版本｜移植：`public async Task<GameAccount?> GetGameAccountFromRegistryAsync(GameBiz biz, CancellationToken cancellationToken = default)`｜理由：Linux 读注册表可能 fork `wine reg query`，UI 线程必须异步（D72/L-127）。
- **[移植 第 47-52 行 · `GetGameAccountFromRegistryAsync` 取 ADL 票据]** 平台替换｜功能损失：否｜建议：保留｜上游：L37-38 `Registry.GetValue(key, keyName, null) as byte[]`｜移植：L49 `string? prefix = Starward.Features.GameSetting.GameSettingService.GetGameWinePrefix(biz);` → L50 `var values = await WineRegistry.GetKeyValuesAsync(key, prefix, cancellationToken)`（整键一次读回）→ L51-52 `values.TryGetValue(keyName, out object? raw); if (raw is byte[] adl)`｜理由：值在游戏所用 wine 前缀的 `user.reg` 里；整键读把 3-4 次 fork 压到 1 次，且 `WineRegistryFiles` 文件快路径毫秒级返回。类型成立：`RegistryFormat.cs:98-103` 把 REG_BINARY 解析成 `byte[]`、REG_DWORD 成 `int`。
- **[移植 第 63-67 行 · 同上，bh3 的 Uid]** 平台替换｜功能损失：否｜建议：保留｜上游：L49 `account.Uid = (int)(Registry.GetValue(GENERAL_DATA_V2_LastLoginUserId_h47158221, 0) ?? 0);`｜移植：`values.TryGetValue(...) ; if (uidValue is int uid) { account.Uid = uid; }`｜理由：缺失时上游写 0、移植保持 `GameAccount.Uid` 默认 0，等价；`WineRegistryFiles.cs:215` 保证 dword 读成 `int`（不会因 `long` 匹配失败而丢 Uid）。
- **[移植 第 71-79 行 · 同上，hk4e 的 `__LastUid___h2153286551`]** 平台替换｜功能损失：否｜建议：保留｜上游：L53-61 取 byte[] → 转字符串 → `long.TryParse`｜移植：同样逻辑，只是数据来源换成字典｜理由：解析细节（去 `\0`、Trim、TryParse）与上游一致。
- **[移植 第 83-87 行 · 同上，hkrpg 的 `App_LastUserID_h2841727341`]** 平台替换｜功能损失：否｜建议：保留｜上游：L65 `(int)(Registry.GetValue(...) ?? 0)`｜移植：`TryGetValue` + `is int`｜理由：同 L63-67。
- **[移植 第 109-119 行 · `GetGameAccounts` / `GetGameAccountsAsync`]** 移植新增｜功能损失：否｜建议：保留｜上游：L73-88 单个同步 `GetGameAccounts`｜移植：同步包装 + `GetGameAccountsAsync`，内部 `await GetGameAccountFromRegistryAsync(biz, ct)` 后 `MergeAccounts(...)`｜理由：异步化（同 D72）+ 保留上游签名。
- **[移植 第 123-138 行 · `MergeAccounts`]** 移植新增｜功能损失：否｜建议：保留｜上游：L91-102 同段逻辑内联在 `GetGameAccounts` 里｜移植：抽成 `private static List<GameAccount> MergeAccounts(List<GameAccount> databaseAccounts, GameAccount? regAccount)`｜理由：纯提炼，合并规则（DB 为准、注册表账号补上/更新）逐行相同。
- **[移植 第 171-177 行 · `ChangeGameAccount` / `ChangeGameAccountAsync`]** 移植新增｜功能损失：否｜建议：保留｜上游：L137 起同步 `ChangeGameAccount`｜移植：同步包装（L171-172）+ `public async Task ChangeGameAccountAsync(GameAccount account, CancellationToken cancellationToken = default)`｜理由：写侧每次 `reg import` 都 fork（冷启 ~1.1 s），必须异步。
- **[移植 第 186-197 行 · `ChangeGameAccountAsync` 聚合待写值]** 平台替换｜功能损失：否｜建议：保留｜上游：L141-158 逐条 `Registry.SetValue`（2-4 次调用）｜移植：先建 `Dictionary<string, object>`：L186 票据、L189 `App_LastUserID_h2841727341 = (int)account.Uid`、L193 bh3 的 GENERAL_DATA_V2、L197 `__LastUid___h2153286551 = Encoding.UTF8.GetBytes($"{account.Uid}\0")`，再一次性 `SetValuesAsync`｜理由：一次 `reg import` 代替逐值写入，值类型经 `RegistryFormat` 生成与上游逐值写等价（含 hk4e 的 `\0` 结尾二进制）。
- **[移植 第 200-210 行 · `ChangeGameAccountAsync` 前缀解析与失败处理]** 平台替换 + 移植新增（bugfix）｜功能损失：否｜建议：保留｜上游：`Registry.SetValue` 失败抛异常，前缀概念不存在｜移植：L200 解析游戏前缀；L203 为空抛 `Lang.Lunarward_Error_PrefixNotFound`；L205 `bool ok = await WineRegistry.SetValuesAsync(...)`；L209 false 抛 `Lang.Lunarward_Error_SetGameSettingFailed`｜理由：必须写进游戏真正的前缀；失败要让用户看见（B40，`GameAccountSwitcher.xaml.cs:103` 弹 toast）。语言键存在（`src/Starward.Language/Lang.Lunarward.cs:152-153`）。
- **[移植 第 49 行 · 读路径 prefix 为 null 时的回落]** 平台替换（残留错位）｜功能损失：是（边缘场景）｜建议：改回上游语义（读侧拒绝默认前缀）/ 待定｜上游：读写同一个 `HKCU\Software\miHoYo\...`，天然一致｜移植：读侧 L49 已改为传 `GetGameWinePrefix(biz)`，但该函数前缀不存在时返回 `null`，`WineRegistry.WinePrefix`（`WineRegistry.cs:46-65`）随即回落 `LUNARWARD_WINEPREFIX` 或 `~/.wine`｜理由：写侧要求真实前缀、读侧在解析失败时改读 `~/.wine`；若游戏绑定了 profile 前缀，账号面板会显示 `~/.wine` 里的旧账号（或空），切号却写进游戏前缀，用户看到「当前账号对不上」。与 `GameSettingService.TryGetReadPrefix`（L343-354，读路径显式拒绝默认前缀，D134）语义也不一致。触发条件：`GetGameWinePrefix` 返回 null（前缀被删/未绑定）时打开账号面板。

### 本文件结论
1. 无「必须立刻修」的功能损失：14 个 hunk 全部是平台替换（Win32 Registry → wine 注册表文件/`reg import`）、异步化、等价重构（`MergeAccounts`）与失败上抛改进，逐条与上游语义对得上（含 Uid 类型、`\0` 结尾二进制、缺失即 0）。
2. 唯一实质风险是读路径 `prefix == null` 回落 `~/.wine`（第 49 行）：与写侧/D134 读侧语义相反，边缘场景下账号面板显示错账号。建议按 `TryGetReadPrefix` 的做法在解析失败时直接返回 null（不读默认前缀），属小改。
3. 附带发现：`docs/linux-port/25-deviation-register.md:519-560` 里「第 47 行 读路径回落 `~/.wine`」那条已过期（代码已在 602475c5 / D134 批 A3 改为传前缀），残留只剩 null 回落，需改文档而非改代码。

## GameAccountSwitcher.xaml.cs
- 变更行数：28（删除 8 / 新增 10），10 个 hunk
- 编译状态：参与编译（不在 `<Compile Remove>` 表内）；无 `*.Linux.cs` 覆盖；同目录 `GameAccountSwitcher.xaml` 与上游逐字节相同

### 差异清单

- **[移植 第 36-39 行 · `GameAccountSwitcher_Loaded`]** 平台替换/工程性｜功能损失：否｜建议：保留｜上游：`private void GameAccountSwitcher_Loaded(...)` 内调同步 `UpdateGameAccount()`｜移植：`private async void GameAccountSwitcher_Loaded(...)` 内 `await UpdateGameAccountAsync();`｜理由：`GetGameAccounts` 现在要读 wine 前缀注册表，同步调用会冻住整窗（L-127 / 硬规则 10）；`async void` 只用于事件处理器，内部 try/catch 由被调方法兜住。
- **[移植 第 73-85 行 · `UpdateGameAccountAsync`]** 平台替换｜功能损失：否｜建议：保留｜上游：`private void UpdateGameAccount()`，`:77 GameAccountList = _gameAccountService.GetGameAccounts(CurrentGameBiz);`｜移植：`public async Task UpdateGameAccountAsync()`，`:77 GameAccountList = await _gameAccountService.GetGameAccountsAsync(CurrentGameBiz);`｜理由：同上，纯粹异步化，其余语句（排序、`CanChangeGameAccount`、选中项恢复）逐行相同。`UpdateGameAccount` 在移植侧已无外部调用点，仅本文件调用 Async 版。
- **[移植 第 88-100 行 · `ChangeGameAccountAsync`（`[RelayCommand]`）]** bugfix + 平台替换｜功能损失：否｜建议：保留｜上游：`:89 private void ChangeGameAccount()` + `:95 _gameAccountService.ChangeGameAccount(SelectGameAccount);`，`catch` 只 `LogError`｜移植：`private async Task ChangeGameAccountAsync()` + `await ...ChangeGameAccountAsync(SelectGameAccount)`｜理由：Linux 侧新增「前缀缺失」「reg import 失败」两类失败源必须异步 + 可见（B40）。命令名不变：`[RelayCommand]` 会去掉 `Async` 后缀，生成 `ChangeGameAccountCommand`，与 XAML `:57` 的 `{x:Bind ChangeGameAccountCommand}` 匹配。
- **[移植 第 101-103 行 · `ChangeGameAccountAsync` 的 catch]** 移植新增（bugfix）｜功能损失：否（对用户是收益）｜建议：保留｜上游：`catch (Exception ex) { _logger.LogError(ex, ...); }`，界面看起来像成功｜移植：`LogError` + `Helpers.InAppToast.MainWindow?.Error(ex.Message, null, 6000);`｜理由：B40 修的就是「静默失败」；`InAppToast.Error(string? title, string? message = null, int duration = 5000)` 重载存在（`Helpers/InAppToast.cs:156`）。残留：`MainWindow` 为 null 时（窗口未起）仍静默，可接受。
- **[移植 第 147-162 行 · `DeleteGameAccountAsync`]** 平台替换｜功能损失：否｜建议：保留｜上游：`:145 private void DeleteGameAccount()`，删完调同步 `UpdateGameAccount()`｜移植：`private async Task DeleteGameAccountAsync()`，`:155 await UpdateGameAccountAsync();`｜理由：删除本身只碰 SQLite，但后续刷新已异步化，整条链保持在后台；命令名 `DeleteGameAccountCommand` 不变（XAML `:132`）。

### 本文件结论
1. 无功能损失：10 个 hunk 是同一根因（`GameAccountService` 变 async）的连锁改动 + 一个上游缺陷修复（失败提示）。
2. XAML、命令名、`CanChangeGameAccount` 门控、`SaveGameAccountAsync`（上游本就是 async，未改）均保持不变，UI 行为对等。
3. 唯一可讨论点：失败提示依赖 `InAppToast.MainWindow` 非空；如要更稳可改为 `InfoBar`，但不属与上游对齐的缺口。

## WindowsDeviceFpService.cs
- 变更行数：452（删除 198 / 新增 189），65 个 hunk（全部逐条判定，按功能块归并展示，hunk 与移植行号一一对应）
- 编译状态：参与编译（不在 `<Compile Remove>` 表内）；无 `*.Linux.cs` 覆盖（`src/Lunarward/Features/Passport/` 下无任何 `.Linux.cs`）

### 差异清单

- **[移植 第 1-4 行 · 文件头]** 平台缺失｜功能损失：否｜建议：保留｜上游：L1-2 `using Microsoft.UI.Windowing; using Microsoft.Win32;`｜移植：`// LUNARWARD-DEVIATION` 说明头 + `using Microsoft.Extensions.Logging;`｜理由：Win32 注册表 / Windowing 在 Linux 不存在。
- **[移植 第 15 行附近 · using 区]** 平台缺失｜功能损失：否｜建议：保留｜上游：L14-15 `using Vanara.PInvoke; using Windows.Graphics;`（DXGI/SMBIOS/DisplayArea）｜移植：删除｜理由：整条 DXGI/SMBIOS 取数路径被 sysfs 取代。
- **[移植 第 22-25 行 · 字段：`SdkStorePath`]** 平台替换｜功能损失：否｜建议：保留｜上游：L25-35 六个注册表键常量（`SdkKey`=HKCU\Software\miHoYoSDK、`CpuKey`、`BiosKey`、`GpuClassKey`、`MachineGuidKey`、`WindowsVersionKey`）｜移植：`private static readonly string SdkStorePath = Path.Combine(AppConfig.CacheFolder, "device-sdk.json");`｜理由：SDK 缓存值改存 JSON（原子写），Linux 无 HKCU\Software\miHoYoSDK。注意 `static readonly` 在首次触碰类型时求值，依赖 `AppConfig.CacheFolder` 已初始化（`AppConfig.Configuration.cs:114-135`，DI 构造在其后）→ 风险低，但若将来有静态/更早路径调用会拿到 null 路径，留一个「待定·观察」。
- **[移植 第 27 行 · 字段：`SdkStoreLock`]** 移植新增｜功能损失：否｜建议：保留｜上游：无（注册表读写本身线程安全）｜移植：`private static readonly object SdkStoreLock = new();`｜理由：JSON 文件读改写需要互斥，替代注册表的事务性。
- **[移植 第 31 行 · 字段：`_logger`]** 死代码｜功能损失：否｜建议：删除｜上游：无该字段｜移植：`private readonly ILogger<WindowsDeviceFpService> _logger = AppConfig.GetLogger<WindowsDeviceFpService>();`，全文再无引用｜理由：唯一出现处即声明；`CS0414`（赋值未使用）被项目 NoWarn 吞掉所以不报警，留着会误导后来者以为这里会写日志。
- **[移植 第 64、71、73、82 行 · 类/属性文档注释]** 文案外观｜功能损失：否｜建议：保留｜上游：注释里写「取注册表缓存」「SDK 注册表键」｜移植：改为「缓存」｜理由：实现已不是注册表，注释跟随实现，无语义影响。
- **[移植 第 96 行 · `CreateRequest` 内注释行]** 死代码（上游即为注释）｜功能损失：否｜建议：保留（无害）｜上游：L105 `//SaveSdkValueIfMissing("MIHOYOSDK_DEVICE_FP", request.DeviceFp);`｜移植：删除该注释行｜理由：上游本来就是注释掉的代码，删除零影响；将来同步上游时可能再次出现，属噪音。
- **[移植 第 116 行 · `CollectExtFields`：`cpuName`]** 平台替换｜功能损失：否｜建议：保留｜上游：`Registry.GetValue(CpuKey, "ProcessorNameString")`｜移植：`ReadCpuInfo("model name") ?? "unknown"`｜理由：`/proc/cpuinfo` 的 `model name` 与 `ProcessorNameString` 同义（含型号串）。
- **[移植 第 118-120 行 · `CollectExtFields`：`cpuFrequency` / `cpuType` / `deviceModel`]** 平台替换（含内容语义变化）｜功能损失：是（低）｜建议：保留 / 待定｜上游：`cpuFrequency`=REG_SZ 的 `~MHz`（整数形如 `3600`），`cpuType`="unknown" 硬编码，`deviceModel`=`BiosKey\BaseBoardProduct`｜移植：`cpuFrequency`=`/proc/cpuinfo` 的 `cpu MHz`（形如 `3600.000`，字符串长度与格式都变了）、`cpuType`=`scaling_driver`（`intel_pstate`/`acpi-cpufreq`）、`deviceModel`=`/sys/class/dmi/id/board_name` ?? `product_name`｜理由：三项都是设备指纹的附加字段，本地无法验证 HYP 服务端是否对这些字符串做一致性校验；`cpuFrequency` 从整数变浮点串是形态变化（同一台机器两次采集一致，不影响稳定性），`cpuType` 反而比上游的 `"unknown"` 有信息量。风险等级：低，记录在案即可。
- **[移植 第 153-169 行 · `GetSdkValue`]** 平台替换｜功能损失：否｜建议：保留｜上游：L164-169 `Registry.GetValue(SdkKey, name, null) as string`，含 `catch { return null; }`｜移植：`lock (SdkStoreLock)` + `ReadStore()?.TryGetValue` + `IsNullOrWhiteSpace` 过滤 + 异常吞掉返回 null｜理由：等价语义，且多一层空串过滤（上游把空串当有效值）。
- **[移植 第 172-194 行 · `SaveSdkValueIfMissing`]** 平台替换｜功能损失：否｜建议：保留｜上游：L181-183 `Registry.SetValue(..., RegistryValueKind.String)` + `catch { }`｜移植：先 `GetSdkValue` 判存在 → 读 JSON → 写 `<path>.tmp` → `File.Move(temp, SdkStorePath, true)` 原子替换 + `Directory.CreateDirectory`｜理由：「缺则写」语义相同；原子替换避免半截文件把指纹读空（比上游更强）。
- **[移植 第 197、200-207 行 · `SdkJsonOptions` / `ReadStore`]** 移植新增｜功能损失：否｜建议：保留｜上游：无对应物（注册表 API 自带持久化）｜移植：`JsonSerializerOptions { WriteIndented = true }` + `File.Exists` 判存 + `Deserialize<Dictionary<string,string>>`｜理由：`GetSdkValue` 的存储底座；反序列化异常由调用方 try/catch 兜住。
- **[移植 第 210-216 行 · `GetMachineGuid`]** 平台替换｜功能损失：否｜建议：保留｜上游：L189 `Registry.GetValue(MachineGuidKey, "MachineGuid", null)`（HKLM\SOFTWARE\Microsoft\Cryptography）｜移植：`/etc/machine-id` ?? `/var/lib/dbus/machine-id` ?? ""｜理由：Linux 的等价「机器唯一 ID」就是 machine-id；两个路径覆盖 systemd 与 dbus 两代布局。
- **[移植 第 220-235 行 · `GenerateDeviceId`]** 平台替换｜功能损失：否（可能换新设备号）｜建议：保留｜上游：L194-209 SMBIOS Type2 主板序列号 + Type1 BIOS 序列号 + `Windows ProductId`，缺失退化 MachineGuid｜移植：`/sys/class/dmi/id/board_serial` + `product_serial` + machine-id 做 SHA1（两分支 L230/L235 都以 machine-id 作兜底）｜理由：DMI 序列号在 Linux 可直读（无需 SMBIOS 解析），SHA1 40 位小写十六进制格式不变。语义差异：输入少一项 `ProductId`，所以同一台双系统机器在 Windows/Linux 会得到不同 deviceId——本来就是平台级指纹，可接受。
- **[移植 第 247-262 行 · `GetOsVersion`]** 平台替换｜功能损失：否｜建议：保留｜上游：L221-224 `$"Windows {Major}.{Minor}.{Build}"`｜移植：读 `/etc/os-release` 的 `PRETTY_NAME`（去引号），失败退 `$"Linux {Environment.OSVersion.Version}"`｜理由：字段语义是「操作系统名称+版本」，Linux 无 Build 概念。
- **[移植 第 267 行 · GPU 段文档注释]** 文案外观｜功能损失：否｜建议：保留｜上游：「取值方式和 SDK 一致：DXGI 拿适配器信息，D3D11 拿特性等级」｜移植：改为 sysfs 说明｜理由：跟随实现。
- **[移植 第 274-285 行 · `GetGpuInfo`]** 平台替换（含字段内容变化）｜功能损失：是（低）｜建议：保留 / 待定｜上游：`TryGetGpuInfoFromDxgi` 取 VendorId/DeviceId/Description，`gpuVersion`="Direct3D 11.0"、`gpuAPI`="Direct3D 11"｜移植：读 `/sys/class/drm/card0/device/{vendor,device}` → `ParseHexId`；`gpuVersion`/`gpuAPI` 都改成 `"OpenGL"`；vendor/device 输出十进制字符串｜理由：字段结构对齐（十进制与上游 `ToString()` 一致），但 `gpuVersion/gpuAPI` 的**内容**从 D3D 变成 OpenGL——HYP 服务端是否只做记录无法本地验证；同一机器上值恒稳定，不影响登录成功率。另 `ParseHexId(deviceId) is uint device and not 0` 中 `is uint` 恒真（写法冗余，等价 `!= 0`），不影响语义。
- **[移植 第 289-318 行 · 新增 `GetGpuNameAndMemory`（取代上游 `TryGetGpuInfoFromDxgi` L321-355）]** 平台替换 + 行为丢失｜功能损失：是（低-中）｜建议：改（修两处）｜上游：DXGI `EnumAdapters(0)` → `Adapter.Description1.Description` 取型号，`DedicatedVideoMemory` 取显存｜移植：优先读 `/proc/driver/nvidia/gpus/*/information` 的 `Model:` 行；显存一律读 `/sys/class/drm/card0/device/mem_info_vram_total`（该节点实为 amdgpu 属性）｜理由：两处真实缺陷——(a) NVIDIA 机器上型号来自 NVIDIA 节点、显存来自 **card0 的 AMD 节点**，多卡/混合显卡时会「NVIDIA 型号 + 别人显存」或双 null；(b) L316 的 `long.Parse(vram)` 若节点内容异常会抛异常，被外层 `catch (Exception) { }` 吞掉后直接 `return (null, null)`，**连 L320 的 AMD 兜底也不再执行**（型号一起丢）。触发条件：NVIDIA 独显（显存恒 unknown 或错配）、sysfs 值非纯数字。影响面：只影响 `gpuName`/`gpuMemory` 两个上报字段，不影响 deviceId/deviceFp 的稳定性。
- **[移植 第 324-329 行 · 删除 DXGI factory 创建（上游 `TryGetGpuInfoFromDxgi` L326-355、`CreateDxgiFactory` L361-376）]** 平台缺失｜功能损失：否｜建议：保留｜上游：依次尝试 `CreateDXGIFactory2/1/` 取适配器｜移植：整段删除｜理由：Linux 无 DXGI；替代路径即上一条的 sysfs/procfs。
- **[移植 第 333-345 行 · `ParseHexId`（取代上游 `CreateDxgiFactory` 段）]** 移植新增｜功能损失：否｜建议：保留｜上游：无（DXGI 直接给数值）｜移植：去 `0x` 前缀 + `uint.TryParse(HexNumber)`，失败返回 0｜理由：sysfs 是十六进制文本；返回 0 与上游「取不到适配器」时语义对齐。
- **[移植 第 348-357 行 · `GetGpuVendorName`]** 无差异（保留判定）｜功能损失：否｜建议：保留｜上游：`0x10DE/0x12D2`→NVIDIA、`0x1002`→AMD、`0x163C/0x8086`→Intel｜移植：逐字相同｜理由：hunk 只落在注释上一行，函数体未变（上游 L380 注释 → 移植 L349）。
- **[移植 第 360-386 行 · `GetMemoryInfo`（取代上游 `GetGpuInfoFromRegistry` L391-417）]** 平台替换｜功能损失：否｜建议：保留｜上游：`HKLM\...\Video` 类枚举显存（`GpuClassKey\0000…`）＋ `Kernel32.GlobalMemoryStatusEx` 取内存｜移植：`/proc/meminfo` 的 `MemTotal` / `MemAvailable` 各 `>> 10` 得 MB｜理由：内存字段与上游同样是 MB 字符串；（显存那半段在 Linux 侧改由 `GetGpuNameAndMemory` 承担，见上）。
- **[移植 第 390-394 行 · `ParseKilobytes`]** 移植新增｜功能损失：否｜建议：保留｜上游：`Kernel32.GlobalMemoryStatusEx` 返回字节｜移植：解析 `/proc/meminfo` 的 `kB` 行（去单位 → long）｜理由：procfs 单位是 kB，`>>10` 后与上游 MB 对齐。
- **[移植 第 397-408 行 · `GetRomInfo`]** 平台替换｜功能损失：否｜建议：保留｜上游：L439-448 系统盘（`Environment.SystemDirectory` 所在卷）容量｜移植：`new DriveInfo(Path.GetPathRoot(AppConfig.CacheFolder) ?? "/")`｜理由：语义是「数据所在磁盘」，Linux 下取数据目录所在挂载点比取 `/` 更贴近「装游戏的盘」；异常有 catch 返回 "unknown"。
- **[移植 第 411-417 行 · `GetScreenSize`]** 行为丢失（可避免）｜功能损失：是（低）｜建议：改（接上已有实现）｜上游：L453 `new DisplayArea(...)` 取 `WxH` 字符串｜移植：直接 `return "unknown";`，注释称「xrandr 会 fork 进程（硬规则 10）」｜理由：全仓已有不 fork 的实现——`Helpers/X11Window.cs:174 public static bool TryGetScreenSize(out int width, out int height)`（libxcb）→ 可以零成本返回真实 `WxH`，`screenSize` 上报字段现恒为空值。注释里的理由与代码事实不符，属可避免偏离。
- **[移植 第 420-439 行 · `GetMacAddress`]** 无差异（保留判定）｜功能损失：否｜建议：保留｜上游：遍历网卡取 MAC，兜底 `"FFFFFFFFFFFF"`｜移植：逐字相同（含兜底）｜理由：hunk 只覆盖函数结束的 `}` 与前后的空行对齐。
- **[移植 第 445-477 行 · 新增 `ReadCpuInfo` / `ReadFile`]** 移植新增｜功能损失：否｜建议：保留｜上游：无（用注册表/Win32）｜移植：`ReadCpuInfo(key)` 扫 `/proc/cpuinfo`首个 `key:` 行；`ReadFile(path)` = `File.Exists` + `ReadAllText().Trim()`，异常吞掉返回 null｜理由：本文件所有 sysfs/procfs 读取的唯一入口，集中兜异常；`ReadFile` 不做大小限制（sysfs 文件都很小，可接受）。

### 本文件结论
1. 必须修的只有一处「可避免缺失」：`screenSize` 恒 `unknown`（第 411-417 行），仓库里已有不 fork 的 `X11Window.TryGetScreenSize`，注释里的「要 fork」理由不成立。
2. 应修（低-中）：`GetGpuNameAndMemory`（第 289-318 行）——NVIDIA 显存读的是 AMD 的 `card0` 节点，且 `long.Parse` 一抛异常就把型号一起丢（`return (null, null)` 连 AMD 兜底都跳过）。
3. 需记录（不改代码）：`gpuVersion`/`gpuAPI` 由 `Direct3D 11.x` 变 `OpenGL`、`cpuFrequency` 由 `3600` 变 `3600.000`、`cpuType` 由硬编码 `unknown` 变驱动名——字段内容变化，服务端是否在意无法本地验证，属平台不可避免的语义偏移。
4. 死代码一处：`_logger`（第 31 行）声明后从未使用。

## HypLoginWindow.xaml.cs
- 变更行数：8（删除 1 / 新增 6），1 个 hunk
- 编译状态：参与编译（不在 `<Compile Remove>` 表内）；无 `*.Linux.cs` 覆盖

### 差异清单

- **[移植 第 71-90 行 · `public new void Activate()`]** 行为丢失（非纯平台缺失）｜功能损失：是（中，真机未验证）｜建议：待定 → 建议在 `Helpers/X11Window` 实现 owner/transient 后再把该处标为「平台替换」｜上游：L73 直接 `User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_HWNDPARENT, ParentWindowHandle);`（把登录窗设为主窗的 owner，Win32 下表现为「始终在主窗之上、随主窗最小化/关闭」）｜移植：L73-78 `// LUNARWARD-DEVIATION` 注释 + `if (OperatingSystem.IsWindows()) { ... }` 守卫，Linux 侧整段不执行；L79-89（`OverlappedPresenter` 的 `IsModal`/`IsMaximizable`/`IsMinimizable`/`IsResizable=false` + `CenterInScreen(520, 740)` + `base.Activate()`）与上游一致｜理由：`WindowHandle` 在 Linux 恒 `IntPtr.Zero`（`Features/WindowBase.Linux.cs:13`），`SetWindowLong` 是 `[DllImport("user32.dll")]`（`Compat/VanaraHotkeyCompat.cs:38`），确实不该执行；但**没有任何替代实现**——全仓搜 `Owner|Transient|WM_TRANSIENT|_NET_WM_STATE` 在 `Helpers/X11Window.cs` 与 `Features/WindowBase.Linux.cs` 均 0 命中。后果（X11 下用户可感知）：登录窗是独立顶层窗，切到主窗后登录窗可能被压在下面；关主窗不会连带关登录窗（`LoginAsync` 的 `finally { Close(); }` 只在自身关闭时跑）；`OverlappedPresenter.IsModal` 在 X11 后端是否拦截主窗交互未知。触发场景：从主界面拉起 HYP 登录 → 点主窗口/主窗被 WM 重新堆叠 → 登录窗被遮挡，用户以为点了没反应。
- **[移植 第 74-77 行 · 同上的注释块]** 文案外观（但内容错误）｜功能损失：否（直接）｜建议：改（同步改文档措辞）｜上游：无注释｜移植：注释称「X11 下窗口 owner 关系由 Helpers/X11Window 处理」｜理由：与实际代码不符——`X11Window` 没有 owner/transient 相关 API，这条注释把「没做」说成了「已在别处做」，会误导后续同步；同样错误的表述在 `docs/linux-port/25-deviation-register.md:2557`。

### 本文件结论
1. 这个文件的唯一差异不是「平台 API 用不了所以跳过」这么简单：`GWL_HWNDPARENT` 带来的 owner/transient 语义在 Linux 侧**完全缺失且无替代**，属行为丢失而非平台替换。
2. 用户可感知风险：登录窗可能被主窗遮挡、不随主窗关闭、模态语义缺失；真机登录流程文档里仍是「待验收」（AGENTS.md §8），所以现在无法断言严重度，但按代码事实不能判为「仅格式或轻微」。
3. 处置：先在真机确认是否真的会被遮挡；确认后在 `Helpers/X11Window` 用 libxcb `InternAtom("WM_TRANSIENT_FOR")` + `change_property` 补上（`InternAtom` 已具备），再回填本处注释；否则至少把注释与 `25-deviation-register.md:2557` 的「由 X11Window 管」改成「无替代实现，待补」。

## 汇总

按严重度排列（C 组内编号；**必须修** = 有用户可感知的功能损失或明确的实现错位）：

- **C1｜`HypLoginWindow.xaml.cs:71-78`**　登录窗的 owner/transient 语义在 Linux 侧完全缺失（`GWL_HWNDPARENT` 被守卫掉且无替代），全仓无 `WM_TRANSIENT_FOR` 实现；登录窗可能被主窗遮挡、不随主窗关闭。需真机确认后在 `Helpers/X11Window` 补 owner，并修正 `25-deviation-register.md:2557` 的错误理由。**严重度：中**（功能可见性，登录流程本身仍可用）
- **C2｜`WindowsDeviceFpService.cs:411-417`**　`GetScreenSize()` 恒返回 `"unknown"`，而 `Helpers/X11Window.cs:174 TryGetScreenSize` 已存在且不 fork——注释里的「xrandr 要 fork」不成立，属可避免的字段缺失。**严重度：中低**（零成本可修）
- **C3｜`WindowsDeviceFpService.cs:289-318`**　NVIDIA 分支的显存读 `/sys/class/drm/card0/device/mem_info_vram_total`（AMD 节点）→ 型号/显存错配或恒 `unknown`；`long.Parse` 抛异常被外层吞掉后 `return (null, null)`，连 AMD 兜底与已取到的型号一起丢。**严重度：中低**
- **C4｜`GameAccountService.cs:49`**　读路径 `prefix == null` 时回落 `~/.wine`，写路径要求真实游戏前缀；前缀解析失败时账号面板会显示错账号/空账号（与 `GameSettingService.TryGetReadPrefix` 的 D134 读侧语义相反）。**严重度：低**
- **C5｜`WindowsDeviceFpService.cs:31`**　`_logger` 声明后从未使用（`CS0414` 被 NoWarn 吞掉），死代码，建议删除。**严重度：低**
- **C6｜`WindowsDeviceFpService.cs:284-285、118、119`**　指纹字段内容变化：`gpuVersion/gpuAPI` 由 `Direct3D 11.x` → `OpenGL`，`cpuFrequency` 由 `3600` → `3600.000`，`cpuType` 由 `unknown` → 驱动名。平台不可避免，服务端是否在意无法本地验证，建议登记为已知语义偏移。**严重度：低（记录）**
- **C7｜文档过期（非代码）**：`docs/linux-port/25-deviation-register.md:519-560` 的「GameAccountService 第 47 行 读路径回落 `~/.wine` 属 avoidable」已过期（602475c5 / D134 批 A3 已改为传前缀，残留仅 null 回落＝C4）；`:869-876` 未把 `screenSize`/死 `_logger`/GPU 内容变化拆点；`18-upstream-table.md:43` 把 `HypLoginWindow.xaml.cs` 归为「仅格式或轻微」与事实不符（应为 api 平台 API + 行为丢失）。**严重度：低（文档一致性）**

无「必须立刻改代码才能上线」的崩溃级问题；`GameAccountService`（14 hunk）与 `GameAccountSwitcher`（10 hunk）逐条对齐后无功能损失，`GameAccountService` 的写侧（一次 `reg import` + 失败上抛）、`GameAccountSwitcher` 的 B40 提示都是对上游的等价或更强实现。
