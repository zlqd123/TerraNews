# 泰拉新闻 TerraNews

tModLoader 新闻播报 mod：每天 04:30（游戏内时间）用一条游戏内聊天消息播报今天的世界状况，事件发生时实时插播。

[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3815077538) · 适用 Terraria 1.4.4.9 / tModLoader 1.4.4.9+（2026.9 分支验证）

## 功能

- **每日看板**：04:30 播报渔夫任务鱼（带可悬停的物品图标）、今日月相、随月相轮换的 NPC 特售（染料商/动物学家/油漆工/服装商/机械师/骷髅商人等）
- **事件插播**：沙尘暴/暴风雪（按地表扫描区分）、大风特卖、日食特卖、血月特卖（溶液按腐化/猩红世界分流）
- **旅商货架**：NPC 368 到货时单独播报；货架被外部刷新（其它 mod 的按钮/指令重掷）时当帧立即播报
- **旅商常驻日报**（可选开关）：旅商类 mod 让她永不离场、货架不再重掷时，每个日报时刻随日报播一份当前货架
- **草药提醒**：满月/血月死亡草、雨天水叶草、15:45 火焰花（跟随原版「可采种」判据，同一半日不重复）

## 配置

- 游戏内：设置 → Mod 配置 → TerraNews（全中文标签，保存即生效）
- 专用服务器：手改 `ModConfigs/TerraNews_Config.json` 后重启；首次进世界自动生成默认文件
- 每个播报都支持自定义模板，占位符见配置项提示；含 `[i:物品ID]` 的行需要用一个颜色标签包住整行

## 从源码构建

```
把本目录放到 tModLoader 的 ModSources/TerraNews/
启动 tModLoader 后在「开发 mod」里点 TerraNews → 构建
```

或命令行：

```
tModLoader.dll -server -build "<ModSources>\TerraNews\"
```

目录里的 `build/` 与 `lib/`（本地编译验证用的 csproj 与引用 dll，需自行从 tModLoader 安装目录复制 `tModLoader.dll`、`FNA.dll`、`ReLogic.dll`、`log4net.dll`、`Newtonsoft.Json.dll`）不随仓库分发，构建 mod 本体不需要它们。

## 许可

[AGPL-3.0](LICENSE)
