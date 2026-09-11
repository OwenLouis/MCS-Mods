# CustomNPCSpawn

《觅长生》自定义每 20 年自动补充的 NPC 数量与类型。

## 功能列表

- **自定义生成规则**：通过配置文件设定每种 Type 的生成数量与境界范围，默认规则为原版生成规则增加境界随机。
- **范围随机**：支持数量范围和境界范围随机。
- **生成间隔**：可设置生成间隔年数（默认 20 年）。
- **性别控制**：可设置仅生成男性、仅生成女性或随机。
- **上限保护**：可设置最大 NPC 总数，达到上限后停止生成。

## 安装方法

1. 确保已订阅 **BepInEx** 等前置框架。
2. 在 Steam 创意工坊订阅本 Mod。
3. 启动游戏后，配置文件会自动生成。

## 修改与编译

如需自行修改代码：

1. 克隆本仓库。
2. 使用 Visual Studio 打开 `CustomNPCSpawn.csproj`。
3. 修改代码并编译，将生成的 `CustomNPCSpawn.dll` 放入 `BepInEx/plugins/` 文件夹。

## 配置文件

路径：`BepInEx/config/Yueyehuiling.MCS.CustomNPCSpawn.cfg`

`SpawnRules` 格式：`Type,Level,Count` 或 `Type,minLevel-maxLevel,Count` 或 `Type,minLevel-maxLevel,minCount-maxCount`。

例如：`1,1-3,3-5;2,2,2` 表示生成 3-5 个 Type1（境界 1-3 随机）和 2 个 Type2（境界 2）。

Type 对应门派: 1竹山宗 2金虹剑派 3星河剑派 4离火门 5化尘教 6-14为各种散修等

Level 对应境界：1-3 为炼气期，4-6 为筑基期，7-9 为金丹期，10-12 为元婴期，13-15 为化神期，每个大境界内依次对应初期、中期、后期。

如果安装了 **BepInEx 插件管理器（ConfigurationManager）**，可以在游戏内按 `F1` 直接修改配置，无需手动编辑文件。

## 备注

此说明和本 Mod 大部分代码均由 DeepSeek 生成。

## 许可证

本项目采用 MIT 许可证。游戏版权归开发商所有。
