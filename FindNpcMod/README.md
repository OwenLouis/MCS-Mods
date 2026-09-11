# FindNpcMod

《觅长生》NPC查询与关注 Mod，可通过多种条件快速查找 NPC 并关注其动向。

## 功能列表

- **姓名查询**：输入名字模糊查找 NPC。
- **门派查询**：输入门派名查找该门派所有 NPC。
- **境界查询**：输入境界名查找该境界所有 NPC。
- **物品查询**：以 `:物品名` 格式查找拥有该物品的 NPC，可加品级如 `:5 聚灵阵阵旗`。
- **技能查询**：以 `!技能名` 格式查找可请教该技能的 NPC。
- **双条件查询**：以 `门派 境界` 格式（空格分隔）查找同时满足两个条件的 NPC。
- **关注功能**：点击查询结果右侧的“关注”按钮，NPC 会显示在任务追踪面板下方。
- **显示开关**：可配置是否显示资质和年龄，并据此排序。

## 安装方法

1. 确保已订阅 **BepInEx** 等前置框架。
2. 在 Steam 创意工坊订阅本 Mod。
3. 启动游戏后，配置文件会自动生成。

## 修改与编译

如需自行修改代码：

1. 克隆本仓库。
2. 使用 Visual Studio 打开 `FindNpcMod.csproj`。
3. 修改代码并编译，将生成的 `FindNpcMod.dll` 放入 `BepInEx/plugins/` 文件夹。

## 配置文件

路径：`BepInEx/config/Yueyehuiling.MCS.FindNpcMod.cfg`

可设置快捷键、是否显示资质年龄、关注 NPC 列表等。

如果安装了 **BepInEx 插件管理器（ConfigurationManager）**，可以在游戏内按 `F1` 直接修改配置，无需手动编辑文件。

## 快捷键

| 按键 | 功能 |
|------|------|
| `F9` | 打开 NPC 查询输入框 |

## 备注

本 Mod 修改自 **NeonSkyline** 的 **F10 一键寻人** Mod。

原 Mod 链接：https://steamcommunity.com/sharedfiles/filedetails/?id=2886452861

此说明和本 Mod 大部分代码均由 DeepSeek 生成。

## 许可证

本项目采用 MIT 许可证。游戏版权归开发商所有。