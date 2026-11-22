using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using static Terraria.ModLoader.Logging;

namespace TerraNews;

// Label/Tooltip 两个字面量属性已被 TML 标为过时（鼓励走 hjson 本地化），但仍在生效：
// 值会被自动注册成翻译条目的默认值，中文标签照常显示。这里压掉过时警告，等真废弃再迁。
#pragma warning disable CS0618

// 配置改用 tModLoader 的 ModConfig：游戏内「设置 → Mod 配置 → TerraNews」能直接开窗口改，
// 每项都带中文标签；文件落在 ModConfigs/TerraNews_Config.json。
// 专用服务器没有 UI，手改这个文件后重启生效；客户端在窗口里改完保存会同步给服务器。
// 各项的语义与 TShock 版 TerraNews.json 一一对应，只是文件名和键名遵循 tModLoader 的约定。
public class TerraNewsConfig : ModConfig
{
    // ModConfig 按 <Mod名>_<Config名>.json 落盘，这里把 Config 名压短成 "Config"，
    // 最终文件就是 ModConfigs/TerraNews_Config.json。
    public override bool Autoload(ref string name)
    {
        name = "Config";
        return true;
    }

    // 广播只由服务器发起（单机也算服务器），所以配置是 ServerSide：专用服务器读
    // ModConfigs/TerraNews_Config.json，配置窗口里的改动保存后会同步给服务器。
    public override ConfigScope Mode => ConfigScope.ServerSide;

    // TML 在配置注册成功后才有实例；世界加载前都是 null，调用方用 ?? 兜底。
    public static TerraNewsConfig? Instance => ModContent.GetInstance<TerraNewsConfig>();

    // 手改文件补全用：与 ConfigManager.Load/Save 同一套路径和文件名。
    public static string FilePath => Path.Combine(Main.SavePath, "ModConfigs", "TerraNews_Config.json");

    // —— 总开关 ——

    [Label("启用")]
    [Tooltip("总开关。关闭后 mod 完全静默。")]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    // —— 功能开关，默认全开 ——

    [Label("每日看板")]
    [Tooltip("04:30 的整条每日播报：月相、渔夫任务鱼、随月相轮换的货架。")]
    [DefaultValue(true)]
    public bool DailyQuestBoard { get; set; } = true;

    [Label("任务鱼图标")]
    [Tooltip("日报里任务鱼的可交互图标（[i:物品ID]，悬停可看鱼的名称与钓法）。关闭后 {icon} 为空。")]
    [DefaultValue(true)]
    public bool QuestFishIcon { get; set; } = true;

    [Label("渔夫状态")]
    [Tooltip("渔夫当前状态文本（仅自定义模板用到 {angler} 时生效）。")]
    [DefaultValue(true)]
    public bool AnglerStatus { get; set; } = true;

    [Label("月相")]
    [Tooltip("今日月相文本。关闭后 {moon}、{moon_bonus} 为空。")]
    [DefaultValue(true)]
    public bool MoonPhase { get; set; } = true;

    [Label("月相特售")]
    [Tooltip("随月相轮换的货架（染料商、动物学家、油漆工、服装商、机械师、骷髅商人等）。关闭后 {sale} 为空。")]
    [DefaultValue(true)]
    public bool MoonSale { get; set; } = true;

    [Label("沙尘暴播报")]
    [Tooltip("沙尘暴/暴风雪开始时播报一条（按地形扫描区分，见「风暴判定」）。")]
    [DefaultValue(true)]
    public bool Sandstorm { get; set; } = true;

    [Label("大风播报")]
    [Tooltip("大风天（HappyWindyDay，仅白天）开始时播报商人风车。")]
    [DefaultValue(true)]
    public bool WindyDay { get; set; } = true;

    [Label("日食播报")]
    [Tooltip("日食开始时播报蒸汽朋克人/机器侠货架。")]
    [DefaultValue(true)]
    public bool Eclipse { get; set; } = true;

    [Label("血月播报")]
    [Tooltip("血月开始时播报各 NPC 货架（树妖毒粉/种子、溶液按腐化/猩红世界分流）。")]
    [DefaultValue(true)]
    public bool BloodMoon { get; set; } = true;

    [Label("满月草药提醒")]
    [Tooltip("满月夜提醒采收死亡草。血月正逢满月的那一晚只发血月那条，同一晚不连发两条。")]
    [DefaultValue(true)]
    public bool FullMoonHerb { get; set; } = true;

    [Label("血月草药提醒")]
    [Tooltip("血月夜提醒采收死亡草，与「血月播报」各自独立成条。")]
    [DefaultValue(true)]
    public bool BloodMoonHerb { get; set; } = true;

    [Label("雨天草药提醒")]
    [Tooltip("开始下雨时提醒采收水叶草（下雨不影响任何货架）。")]
    [DefaultValue(true)]
    public bool RainHerb { get; set; } = true;

    [Label("火焰花提醒")]
    [Tooltip("游戏内 15:45 提醒采收火焰花（原版判据 time>40500，15:45 起可采种），每天只发一条，过了不补播。")]
    [DefaultValue(true)]
    public bool FireblossomHerb { get; set; } = true;

    [Label("旅商货架")]
    [Tooltip("旅商（NPC 368）到货的上升沿单独播一份货架清单，不混进日报；她当天没来就不发。")]
    [DefaultValue(true)]
    public bool TravelingMerchant { get; set; } = true;

    [Label("写入服务端日志")]
    [Tooltip("播报内容同时镜像一份到服务端日志（图标写成 [物品#ID]）。")]
    [DefaultValue(true)]
    public bool ServerLog { get; set; } = true;

    // —— 时间参数 ——

    [Label("播报小时")]
    [Tooltip("每日播报的钟点（游戏内时间）。04:30 正是渔夫换新任务的时刻。")]
    [Range(0, 23)]
    [DefaultValue(4)]
    public int BroadcastHour { get; set; } = 4;

    [Label("播报分钟")]
    [Tooltip("每日播报的分钟（游戏内时间）。")]
    [Range(0, 59)]
    [DefaultValue(30)]
    public int BroadcastMinute { get; set; } = 30;

    [Label("触发窗口")]
    [Tooltip("04:30 之后多久内仍算触发窗口，单位是游戏分钟（键名带「秒」是历史包袱）。默认 30 即覆盖 04:30-05:00。空服时段世界不推进，不会触发。")]
    [Range(1, 120)]
    [DefaultValue(30)]
    public int TriggerWindowSeconds { get; set; } = 30;

    [Label("启动延迟")]
    [Tooltip("mod 加载后保持静默多久（真实秒），避免开服瞬间连播。")]
    [Range(0, 60)]
    [DefaultValue(5)]
    public int StartupDelaySeconds { get; set; } = 5;

    [Label("看板延迟秒数")]
    [Tooltip("04:30 触发后先等这么多现实秒，再重新检测世界状态并发布——渔夫任务与月相货都在黎明那一帧才换好。设 0 则触发帧立即播报。")]
    [Range(0, 60)]
    [DefaultValue(2)]
    public int DailyDelaySeconds { get; set; } = 2;

    [Label("旅商延迟秒数")]
    [Tooltip("旅商到货的上升沿出现后先等这么多现实秒再播报，确保读到她这一趟掷好的货架。设 0 则到货帧立即播报。货架被指令/按钮外部刷新时当帧立即播报，不受此延迟影响。")]
    [Range(0, 60)]
    [DefaultValue(2)]
    public int MerchantDelaySeconds { get; set; } = 2;

    [Label("旅商常驻日报")]
    [Tooltip("旅商类 mod 让她永不离场时，原版不再重掷她的货架（重掷只发生在刷出她之前），「货架变了才播」就永远轮不到。开启后每个日报时刻（04:30）若旅商在场，随日报一起播一份她当前在卖的货架；她恰好在窗口内到货或货架刚被刷新时两路合并，仍只播一条。")]
    [DefaultValue(false)]
    public bool MerchantDailyBoard { get; set; }

    [Label("最小日报间隔")]
    [Tooltip("两份日报之间至少隔多少真实秒，兜住异常情况下的连播；正常游戏一天 15 分钟真实时间，远大于默认值。")]
    [Range(0, 3600)]
    [DefaultValue(90)]
    public int MinimumAnnouncementGapSeconds { get; set; } = 90;

    [Label("风暴判定")]
    [OptionStrings(new[] { "auto", "sandstorm", "blizzard" })]
    [Tooltip("原版沙尘暴与暴风雪是同一个事件，按地形区分：auto=扫描地表明层数沙/雪谁多；sandstorm=一律按沙尘暴播；blizzard=一律按暴风雪播。")]
    [DefaultValue("auto")]
    public string StormType { get; set; } = "auto";

    [Label("诊断日志")]
    [Tooltip("每现实秒输出一行触发器正在观察的状态，用于验收排查。")]
    [DefaultValue(false)]
    public bool Diagnostics { get; set; }

    // —— 模板 ——

    [Label("每日看板模板")]
    [Tooltip("占位符：{icon} 任务鱼图标、{angler} 渔夫状态、{id} 鱼 ID、{moon} 月相、{sale} 月相特售、{time} 时刻。含 [i:物品ID] 的行必须用「一个颜色标签包住整行」，否则图标会变成一串字符。")]
    public List<string> DailyLines { get; set; } = new List<string>
    {
        "[c/4FC3F7:泰拉日报：{moon}；渔夫：{icon}；{sale}]"
    };

    [Label("旅商到货模板")]
    [Tooltip("旅商到货后的独立播报。占位符：{merchant_items} 货架图标行、{merchant} 带店名的一行、{count} 件数、{time}、{moon}。")]
    public List<string> MerchantLines { get; set; } = new List<string>
    {
        "[c/4FC3F7:旅商来了！今日货架：{merchant_items}]"
    };

    [Label("沙尘暴模板")]
    [Tooltip("占位符：{storm}、{time}、{moon}。")]
    public List<string> SandstormLines { get; set; } = new List<string> { "[c/E0A458:特别报道：沙尘暴]" };

    [Label("暴风雪模板")]
    [Tooltip("占位符：{storm}、{time}、{moon}。沙尘暴与暴风雪是同一个事件，按地形扫描或「风暴判定」区分。")]
    public List<string> BlizzardLines { get; set; } = new List<string> { "[c/E0A458:特别报道：暴风雪]" };

    [Label("大风模板")]
    [Tooltip("占位符：{items} 货架、{count} 件数、{time}、{moon}。")]
    public List<string> WindyLines { get; set; } = new List<string> { "[c/9AD4C8:特别报道：大风特卖，{items}]" };

    [Label("日食模板")]
    [Tooltip("占位符：{items} 货架、{count} 件数、{time}、{moon}。溶液按腐化/猩红世界分流。")]
    public List<string> EclipseLines { get; set; } = new List<string> { "[c/B39DDB:特别报道：日食特卖，{items}]" };

    [Label("血月模板")]
    [Tooltip("占位符：{items} 货架、{count} 件数、{time}、{moon}。")]
    public List<string> BloodMoonLines { get; set; } = new List<string> { "[c/8B0000:特别报道：血月特卖，{items}]" };

    [Label("满月草药模板")]
    [Tooltip("占位符：{time}、{moon}。")]
    public List<string> FullMoonHerbLines { get; set; } = new List<string>
    {
        "[c/9CB767:草药提醒：今夜满月，[i:316]死亡草开花了，记得采种]"
    };

    [Label("血月草药模板")]
    [Tooltip("占位符：{time}、{moon}。")]
    public List<string> BloodMoonHerbLines { get; set; } = new List<string>
    {
        "[c/8B0000:草药提醒：血月夜，[i:316]死亡草开花了，记得采种]"
    };

    [Label("雨天草药模板")]
    [Tooltip("占位符：{time}、{moon}。")]
    public List<string> RainHerbLines { get; set; } = new List<string>
    {
        "[c/4FC3F7:草药提醒：下雨了，[i:317]水叶草开花了，记得采种]"
    };

    [Label("火焰花模板")]
    [Tooltip("占位符：{time}、{moon}。火焰花 15:30-19:30（游戏内）开花、15:45 起可采种，泡在岩浆里随时可采。")]
    public List<string> FireblossomHerbLines { get; set; } = new List<string>
    {
        "[c/E2621B:草药提醒：{time}，[i:318]火焰花开花了，记得采种]"
    };

    public override void OnLoaded()
    {
        Sanitize();
    }

    // UI 的 [Range] 只约束窗口输入；手改文件绕得过去，加载后夹一遍。
    public void Sanitize()
    {
        BroadcastHour = Math.Clamp(BroadcastHour, 0, 23);
        BroadcastMinute = Math.Clamp(BroadcastMinute, 0, 59);
        TriggerWindowSeconds = Math.Clamp(TriggerWindowSeconds, 1, 120);
        StartupDelaySeconds = Math.Clamp(StartupDelaySeconds, 0, 60);
        DailyDelaySeconds = Math.Clamp(DailyDelaySeconds, 0, 60);
        MerchantDelaySeconds = Math.Clamp(MerchantDelaySeconds, 0, 60);
        MinimumAnnouncementGapSeconds = Math.Clamp(MinimumAnnouncementGapSeconds, 0, 3600);
        if (string.IsNullOrWhiteSpace(StormType)) StormType = "auto";

        if (DailyLines is null || DailyLines.Count == 0)
            DailyLines = new List<string> { "[c/4FC3F7:泰拉日报：{moon}；渔夫：{icon}；{sale}]" };
        if (MerchantLines is null || MerchantLines.Count == 0)
            MerchantLines = new List<string> { "[c/4FC3F7:旅商来了！今日货架：{merchant_items}]" };
        if (SandstormLines is null || SandstormLines.Count == 0)
            SandstormLines = new List<string> { "[c/E0A458:特别报道：沙尘暴]" };
        if (BlizzardLines is null || BlizzardLines.Count == 0)
            BlizzardLines = new List<string> { "[c/E0A458:特别报道：暴风雪]" };
        if (WindyLines is null || WindyLines.Count == 0)
            WindyLines = new List<string> { "[c/9AD4C8:特别报道：大风特卖，{items}]" };
        if (EclipseLines is null || EclipseLines.Count == 0)
            EclipseLines = new List<string> { "[c/B39DDB:特别报道：日食特卖，{items}]" };
        if (BloodMoonLines is null || BloodMoonLines.Count == 0)
            BloodMoonLines = new List<string> { "[c/8B0000:特别报道：血月特卖，{items}]" };
        if (FullMoonHerbLines is null || FullMoonHerbLines.Count == 0)
            FullMoonHerbLines = new List<string> { "[c/9CB767:草药提醒：今夜满月，[i:316]死亡草开花了，记得采种]" };
        if (BloodMoonHerbLines is null || BloodMoonHerbLines.Count == 0)
            BloodMoonHerbLines = new List<string> { "[c/8B0000:草药提醒：血月夜，[i:316]死亡草开花了，记得采种]" };
        if (RainHerbLines is null || RainHerbLines.Count == 0)
            RainHerbLines = new List<string> { "[c/4FC3F7:草药提醒：下雨了，[i:317]水叶草开花了，记得采种]" };
        if (FireblossomHerbLines is null || FireblossomHerbLines.Count == 0)
            FireblossomHerbLines = new List<string> { "[c/E2621B:草药提醒：{time}，[i:318]火焰花开花了，记得采种]" };
    }

    // 把开关对象包装成与 TShock 版共用的访问器，模板渲染那边就不用改。
    public FeatureSwitches ToSwitches() => new FeatureSwitches
    {
        DailyQuestBoard = DailyQuestBoard,
        QuestFishIcon = QuestFishIcon,
        AnglerStatus = AnglerStatus,
        MoonPhase = MoonPhase,
        MoonSale = MoonSale,
        Sandstorm = Sandstorm,
        WindyDay = WindyDay,
        Eclipse = Eclipse,
        BloodMoon = BloodMoon,
        FullMoonHerb = FullMoonHerb,
        BloodMoonHerb = BloodMoonHerb,
        RainHerb = RainHerb,
        FireblossomHerb = FireblossomHerb,
        TravelingMerchant = TravelingMerchant,
        ServerLog = ServerLog
    };

    public string Summary() => ToSwitches().Summary();

    // 专用服务器上没有配置窗口，文件只有被保存过才会存在。首次进世界时补写一份
    // 默认值，服主才知道该去手改哪个文件；已存在的文件不动（那是服主改过的）。
    // 手改后重启生效；配置窗口里改则即时生效，不用重启。
    public static void WriteDefaultFileIfMissing()
    {
        try
        {
            string path = FilePath;
            if (File.Exists(path) || Instance is not { } cfg)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonConvert.SerializeObject(cfg, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            }), new UTF8Encoding(false));
            PublicLogger.Info($"[TerraNews] 已生成默认配置文件 {path}，手改后重启生效；也可在游戏内「设置 → Mod 配置」直接改。");
        }
        catch (Exception ex)
        {
            PublicLogger.Warn($"[TerraNews] 默认配置文件写入失败（{ex.Message}），不影响播报。");
        }
    }
}
