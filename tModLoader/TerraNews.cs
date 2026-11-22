using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static Terraria.ModLoader.Logging;

namespace TerraNews;

// 泰拉新闻：tModLoader 版。Terraria 1.4.4.9 / tModLoader 1.4.4.9+2026.09.3.1。
//
// tModLoader 的 ModSystem.PostUpdateEverything 挂的就是 Main.Update 内部，和 TShock 的
// ServerApi.Hooks.GameUpdate 同一个位置——而专用服务器只在有客户端连接时才调用
// Main.Update，所以新闻同样只会发往有人的服务器。
//
// v1.1 的变化：
//   - 配置换成 ModConfig（游戏内「设置 → Mod 配置」有窗口，全中文标签；
//     文件在 ModConfigs/TerraNews_Config.json，专用服务器手改后重启生效）
//   - 修复多人客户端上每条消息显示两遍的问题（见 PostUpdateEverything 开头）
//   - 日报在 04:30 触发后先等「看板延迟秒数」再重新检测发布（与 TShock 版一致）
//   - 旅商改为 NPC 368 到货上升沿触发、独立播报（原版是刷出她之前才掷货架，
//     到货后读 Main.travelShop 拿到的必是这一趟的新货）
//   - 新增草药提醒四条：满月/血月死亡草、雨天水叶草、15:45 火焰花
public class TerraNews : Mod
{
    public TerraNews()
    {
    }
}

// 实际干活的 ModSystem。
public class TerraNewsSystem : ModSystem
{
    private HalfDayTrigger _trigger = new();
    private WorldEventWatcher _events = new();

    private DateTime? _modStart;
    private DateTime _lastDiag = DateTime.MinValue;
    private bool _ready;

    // 上一次日报的真实时间，用来给连播去抖。
    private DateTime _lastAnnouncement = DateTime.MinValue;

    // 每日看板的待发时刻：04:30 触发后先等「看板延迟秒数」再发布。
    private DateTime? _pendingDailyAt;

    // 旅商播报的待发时刻：到货上升沿先等「旅商延迟秒数」；货架指纹变化（指令/按钮刷新）当帧就发。
    private DateTime? _pendingMerchantAt;

    // 旅商在场的基线。进世界时她若已在场只记基线、不播报——那不是"变化"。
    private bool _merchantWasPresent;

    // 旅商货架的指纹：捕捉「人不动、货换新」的外部刷新——TShock 的刷新指令、tMod 侧
    // 其他 mod 的按钮都是原地重掷 Main.travelShop、不重生成 NPC，到货沿捕捉不到。
    private string _merchantFingerprint = string.Empty;
    private bool _merchantFingerprinted;
    private DateTime _lastMerchantScan = DateTime.MinValue;

    // 草药提醒的去重按「半日」计：昼夜各算一档。死亡草一晚最多一条
    // （满月与血月共享标记），火焰花一天最多一条。
    private int _herbHalfDay;
    private bool _herbLastDayTime;
    private int _deathweedNotifiedHalfDay = -1;
    private int _fireblossomNotifiedHalfDay = -1;

    // 火焰花的提醒钟点：游戏内 15:45。原版「可采种」判据是白天且 time > 40500
    // （40500/3600 = 11.25 小时，04:30 起算正好 15:45），这里跟着用同一钟点。
    private const double FireblossomMinutes = 15 * 60.0 + 45.0;

    // ModConfig 注册完成前 Instance 是 null，兜一份默认值，命令行/诊断不至于空引用。
    private static readonly TerraNewsConfig _fallbackConfig = new();
    public static TerraNewsConfig Config => TerraNewsConfig.Instance ?? _fallbackConfig;

    private double TriggerWindowTicks => Config.TriggerWindowSeconds * GameTime.TicksPerGameMinute;

    public override void OnWorldLoad()
    {
        // 每个世界都是一轮新的观察：半日闩锁、事件沿、旅商基线、待发队列全部重置。
        _trigger = new HalfDayTrigger();
        _events = new WorldEventWatcher();
        _pendingDailyAt = null;
        _pendingMerchantAt = null;
        _merchantWasPresent = NPC.AnyNPCs(NPCID.TravellingMerchant);
        _merchantFingerprint = string.Empty;
        _merchantFingerprinted = false;
        _lastMerchantScan = DateTime.MinValue;
        _herbHalfDay = 0;
        _herbLastDayTime = Main.dayTime;
        _deathweedNotifiedHalfDay = -1;
        _fireblossomNotifiedHalfDay = -1;
        _modStart = DateTime.UtcNow;
        _ready = false;
        _lastAnnouncement = DateTime.MinValue;
        _worldIsSnowy = null;   // 地形判定只对当前世界成立，换世界要重扫

        TerraNewsConfig.WriteDefaultFileIfMissing();

        if (!Config.Enabled)
        {
            PublicLogger.Warn($"[TerraNews] 已加载，但配置里 启用=false，mod 保持静默。可在「设置 → Mod 配置」或手改 {TerraNewsConfig.FilePath} 后打开。");
            return;
        }

        // TriggerWindowSeconds 这个键名有历史包袱，实际单位是游戏分钟（见 TriggerWindowTicks），
        // 所以窗口终点直接加它、不除 60。
        int windowEnd = (Config.BroadcastHour * 60 + Config.BroadcastMinute + Config.TriggerWindowSeconds) % 1440;
        PublicLogger.Info($"[TerraNews] 已加载。总开关=开；每日播报 {Config.BroadcastHour:00}:{Config.BroadcastMinute:00}（游戏内时间，触发后等 {Config.DailyDelaySeconds} 现实秒发布，窗口 {Config.TriggerWindowSeconds} 游戏分钟，即到 {windowEnd / 60:00}:{windowEnd % 60:00}）。");
        PublicLogger.Info($"[TerraNews] 功能开关：{Config.Summary()}");
        PublicLogger.Info("[TerraNews] 任务鱼图标使用原版聊天物品标签 [i:物品ID]，玩家可悬停查看详情。");
        PublicLogger.Info($"[TerraNews] 月相特售对照表来源：{MoonSale.SourceGameVersion}");

        if (Config.TravelingMerchant)
            PublicLogger.Info($"[TerraNews] 旅商（NPC 368）到货后等 {Config.MerchantDelaySeconds} 现实秒单独播报；货架被指令/按钮外部刷新时当帧立即播报。");
        if (Config.TravelingMerchant && Config.MerchantDailyBoard)
            PublicLogger.Info("[TerraNews] 旅商常驻日报=开：日报时刻旅商在场就随日报播一份当前货架（无论货架是否变化）。");

        if (MoonSale.VersionWarning(Main.versionNumber) is string mismatch)
            PublicLogger.Warn(mismatch);
    }

    // 1.4.4 的 ModSystem 没有 1.4.5 才有的 PostUpdate(GameTime)，对应位置叫
    // PostUpdateEverything()，同样挂在 Main.Update 内部。
    public override void PostUpdateEverything()
    {
        // 双发修复：TML 1.4.4.9 的 ChatHelper.BroadcastChatMessageAs 只按 Main.dedServ
        // 分流——专用服务器发网络包，其它一律本地 DisplayMessage。多人客户端上 mod 若
        // 也跑这一套，玩家会同时收到「服务器包」和「客户端本地」两条一模一样的消息。
        // 广播的权威在服务器（单机/主机也算服务器），客户端只坐着收。
        if (Main.netMode == 1)
            return;

        double time = Main.time;
        bool dayTime = Main.dayTime;
        FeatureSwitches features = Config.ToSwitches();

        if (Config.Diagnostics && (DateTime.UtcNow - _lastDiag).TotalSeconds >= 1.0)
        {
            _lastDiag = DateTime.UtcNow;
            PublicLogger.Info(
                $"[TerraNews][diag] dayTime={dayTime} time={time:0.0} clock={GameClock()} "
                + $"halfDay={_trigger.HalfDayIndex} announced={_trigger.AnnouncedHalfDay} "
                + $"pendingDaily={_pendingDailyAt != null} pendingMerchant={_pendingMerchantAt != null} "
                + $"merchant={NPC.AnyNPCs(NPCID.TravellingMerchant)} "
                + $"quest={CurrentQuestFishId} moon={Main.moonPhase} storm={Sandstorm.Happening} "
                + $"travelShop={Main.travelShop?.Count(id => id > 0) ?? -1}");
        }

        _modStart ??= DateTime.UtcNow;
        bool warmingUp = !_ready && (DateTime.UtcNow - _modStart.Value).TotalSeconds < Config.StartupDelaySeconds;

        if (warmingUp || !Config.Enabled)
        {
            // 事件监视器要照常推进，否则静默期里发生过的风暴、大风、日食、血月会在解除
            // 静默后被当成刚发生而补播一条。每日窗口不消耗：服务器若恰好在窗口内启动，
            // 静默期结束时玩家仍应看到当天看板，把这条吞掉才是真丢信息。
            _trigger.Tick(time, dayTime, Config.BroadcastHour, Config.BroadcastMinute, TriggerWindowTicks);
            TickEvents(features);
            return;
        }

        _ready = true;

        // 1) 每日看板 —— 严格限制在 04:30 窗口内，绝不在当天更晚的时候补播
        if (_trigger.Tick(time, dayTime, Config.BroadcastHour, Config.BroadcastMinute, TriggerWindowTicks))
        {
            // 窗口无论是否播报都会被消耗掉，区别只在于要不要真的发出去。
            _trigger.MarkAnnounced();

            if (features[Feature.DailyQuestBoard])
                _pendingDailyAt = DateTime.UtcNow.AddSeconds(Config.DailyDelaySeconds);

            // 旅商常驻日报：常驻类 mod 挡掉原版离场后货架不再重掷，「变化才播」永远轮不到；
            // 开了开关就在日报时刻旅商在场时一并播一份当前货架。她恰好在窗口内到货时
            // 到货沿已排下待发，??= 不覆盖，两路合并成一次播报，且此刻读到的必是新货。
            if (features[Feature.TravelingMerchant] && Config.MerchantDailyBoard
                && NPC.AnyNPCs(NPCID.TravellingMerchant))
                _pendingMerchantAt ??= DateTime.UtcNow.AddSeconds(Config.DailyDelaySeconds);
        }

        // 日报到点发布：触发帧之后等「看板延迟秒数」再重新检测世界状态——渔夫任务与
        // 月相货架都在黎明那一帧才换好，抢前一帧读到的还是昨天的。
        PublishPendingDaily(features);

        // 2) 旅商到货（NPC 368 上升沿，独立播报）
        TickMerchant(features);

        // 3) 各种一次性事件 + 草药提醒
        TickEvents(features);
    }

    // 每日看板的延迟发布。发布时才拼上下文，所以拿到的必然是延迟过后重新检测的世界状态。
    private void PublishPendingDaily(FeatureSwitches features)
    {
        if (_pendingDailyAt is not { } due || DateTime.UtcNow < due)
            return;

        _pendingDailyAt = null;

        if (!features[Feature.DailyQuestBoard])
            return;

        // 半日闩锁被拨时间绕过时，去抖兜住；正常游戏一天 15 分钟，这里永远不触发。
        double gap = (DateTime.UtcNow - _lastAnnouncement).TotalSeconds;
        if (gap < Config.MinimumAnnouncementGapSeconds)
        {
            PublicLogger.Info(
                $"[TerraNews] 距上一条日报只过了 {gap:0} 秒，不足 {Config.MinimumAnnouncementGapSeconds} 秒，本次跳过。");
            return;
        }

        _lastAnnouncement = DateTime.UtcNow;
        Broadcast(Config.DailyLines, BuildDailyContext(features), features);
    }

    // 旅商播报的双触发，两个源共用同一个待发旗，到点只播一次：
    //   1) 到货上升沿（NPC 368 出现）——原版在刷出她之前才掷 Chest.SetupTravelShop()，
    //      到货后读 Main.travelShop 拿到的必是这一趟的新货；
    //   2) 货架指纹变化（每现实秒看一次）——指令/按钮类的外部刷新是原地重掷
    //      Main.travelShop、不重生成 NPC，只有指纹能捕捉；进世界时只记基线不播报。
    // 到货沿等「旅商延迟秒数」再发布；指纹变化在货架掷完时才可见，当帧立即发布——
    // 但自然到货本身也伴随一次重掷（刷出前 SetupTravelShop 先跑），那一次指纹变化
    // 不覆盖到货沿已排下的延迟。货架被清空（理论上不会）就安静地不发。
    private void TickMerchant(FeatureSwitches features)
    {
        bool present = NPC.AnyNPCs(NPCID.TravellingMerchant);
        bool arrived = present && !_merchantWasPresent;
        _merchantWasPresent = present;

        if (arrived && features[Feature.TravelingMerchant])
            _pendingMerchantAt = DateTime.UtcNow.AddSeconds(Config.MerchantDelaySeconds);

        if ((DateTime.UtcNow - _lastMerchantScan).TotalSeconds >= 1.0)
        {
            _lastMerchantScan = DateTime.UtcNow;
            string fingerprint = BuildMerchantFingerprint(Main.travelShop);

            if (!_merchantFingerprinted)
            {
                _merchantFingerprint = fingerprint;
                _merchantFingerprinted = true;
            }
            else if (fingerprint != _merchantFingerprint)
            {
                string old = _merchantFingerprint;
                _merchantFingerprint = fingerprint;
                // 到货沿总伴随一次重掷；只有「没有待发播报」的指纹变化才是纯外部刷新
                // （指令/按钮），新货已掷好，当帧立即播报，和 v1.0 一样几乎无感。
                bool externalRefresh = false;
                if (features[Feature.TravelingMerchant] && _pendingMerchantAt is null)
                {
                    _pendingMerchantAt = DateTime.UtcNow;
                    externalRefresh = true;
                }
                if (Config.Diagnostics)
                    PublicLogger.Info($"[TerraNews] 旅商货架指纹变化 {Hash(old)} → {Hash(fingerprint)}"
                        + (features[Feature.TravelingMerchant]
                            ? (externalRefresh ? "，立即播报" : "，到货播报已排定，合并进去")
                            : "（旅商货架开关已关，不播报）"));
            }
        }

        if (_pendingMerchantAt is not { } due || DateTime.UtcNow < due)
            return;

        _pendingMerchantAt = null;
        BroadcastMerchantStock(features);
    }

    // 把货架拼成字符串指纹。用逗号分隔，避免 [1, 23] 和 [12, 3] 撞成同一个指纹。
    private static string BuildMerchantFingerprint(int[]? shop)
    {
        if (shop is null)
            return string.Empty;

        var sb = new StringBuilder(shop.Length * 5);
        foreach (int id in shop)
            sb.Append(id).Append(',');

        return sb.ToString();
    }

    // 指纹太长，日志里只给 8 位哈希，够对人眼判断"变没变"。
    private static string Hash(string fingerprint)
        => fingerprint.Length == 0 ? "(空)" : fingerprint.GetHashCode().ToString("X8");

    // 到货延迟到点后播报货架；延迟期间货架被清空（理论上不会）就安静地不发。
    public void BroadcastMerchantStock(FeatureSwitches features)
    {
        if (!features[Feature.TravelingMerchant])
            return;

        var stock = WorldEventWatcher.MerchantStock(Main.travelShop);
        if (stock.Count == 0)
            return;

        Broadcast(Config.MerchantLines, new Dictionary<string, string>
        {
            ["merchant_items"] = WorldEventWatcher.MerchantIconRow(stock),
            ["merchant"] = WorldEventWatcher.MerchantRow(Main.travelShop),
            ["count"] = stock.Count.ToString(),
            ["time"] = GameClock(),
            ["moon"] = MoonPhases.Name(Main.moonPhase)
        }, features);
    }

    // 沙尘暴/暴风雪、大风、日食、血月都只在出现的那一刻播一次；草药提醒也在这里
    // 一起推进——满月夜/血月/下雨是上升沿，火焰花是每天 15:45 后的定点提醒。
    private void TickEvents(FeatureSwitches features)
    {
        bool stormStarted = _events.TickSandstorm(Sandstorm.Happening);
        bool windyStarted = _events.TickWindy(Main.dayTime && Main.IsItAHappyWindyDay);
        bool eclipseStarted = _events.TickEclipse(Main.eclipse);
        bool bloodMoonStarted = _events.TickBloodMoon(Main.bloodMoon);

        if (stormStarted)
            BroadcastStorm(IsBlizzard(), features);

        // 大风只在白天有货，夜里报了就是假消息。
        if (windyStarted && features[Feature.WindyDay])
            BroadcastSale(Config.WindyLines, MoonSale.WindyDay(), features);

        if (eclipseStarted && features[Feature.Eclipse])
            BroadcastSale(Config.EclipseLines, MoonSale.EclipseSale(WorldGen.crimson), features);

        if (bloodMoonStarted && features[Feature.BloodMoon])
            BroadcastSale(Config.BloodMoonLines, MoonSale.BloodMoonSale(WorldGen.crimson), features);

        TickHerbs(features, bloodMoonStarted);
    }

    // 草药提醒四条，与事件播报各自独立成条、独立开关：
    //   - 满月夜死亡草（非血月夜才可能为真）、血月夜死亡草：两条共享「今晚已报」标记，
    //     血月正逢满月的那一晚只发血月那条，同一晚不会连发两条；
    //   - 开始下雨的水叶草；白天 15:45 后的火焰花（每天一条，触发窗口与日报同宽）。
    private void TickHerbs(FeatureSwitches features, bool bloodMoonStarted)
    {
        if (Main.dayTime != _herbLastDayTime)
        {
            _herbHalfDay++;
            _herbLastDayTime = Main.dayTime;
        }

        // 火焰花：白天 15:45 过后、触发窗口之内报一次，窗口过了当天就不补播。
        double elapsed = GameTime.MinutesElapsed(Main.time, Main.dayTime);
        if (Main.dayTime
            && features[Feature.FireblossomHerb]
            && _fireblossomNotifiedHalfDay != _herbHalfDay
            && elapsed >= FireblossomMinutes
            && elapsed < FireblossomMinutes + Config.TriggerWindowSeconds)
        {
            _fireblossomNotifiedHalfDay = _herbHalfDay;
            BroadcastNotice(Config.FireblossomHerbLines, features);
        }

        // 血月草药：血月开始的那一刻。
        if (bloodMoonStarted && features[Feature.BloodMoonHerb] && _deathweedNotifiedHalfDay != _herbHalfDay)
        {
            _deathweedNotifiedHalfDay = _herbHalfDay;
            BroadcastNotice(Config.BloodMoonHerbLines, features);
        }

        // 满月草药：非血月的满月夜开始的那一刻。血月夜判据恒假，所以血月正逢满月的
        // 那一晚只会发血月那条；半夜才起血月的场合靠上面的同晚标记去重。
        if (_events.TickFullMoonNight(!Main.dayTime && Main.moonPhase == 0 && !Main.bloodMoon)
            && features[Feature.FullMoonHerb]
            && _deathweedNotifiedHalfDay != _herbHalfDay)
        {
            _deathweedNotifiedHalfDay = _herbHalfDay;
            BroadcastNotice(Config.FullMoonHerbLines, features);
        }

        // 雨天草药：开始下雨的那一刻。判据与原版「水叶草可采」一致：Main.raining
        // || Main.cloudAlpha > 0（后者是雨刚停、云还没散完的尾巴）。
        if (_events.TickRaining(Main.raining || Main.cloudAlpha > 0f) && features[Feature.RainHerb])
        {
            BroadcastNotice(Config.RainHerbLines, features);
        }
    }

    // 走 {items} 占位符的一次性播报，物品由 MoonSale 的表拼成。
    private void BroadcastSale(List<string> lines, (Shop Shop, Lot[] Lots)[] rows, FeatureSwitches features)
    {
        string items = MoonSale.Render(rows);
        Broadcast(lines, new Dictionary<string, string>
        {
            ["items"] = items,
            ["count"] = rows.Sum(r => r.Lots.Length).ToString(),
            ["time"] = GameClock(),
            ["moon"] = MoonPhases.Name(Main.moonPhase)
        }, features);
    }

    // 独立小条目（草药提醒）的通用播报，占位符只有 {time} 和 {moon}。
    private void BroadcastNotice(List<string> lines, FeatureSwitches features)
    {
        Broadcast(lines, new Dictionary<string, string>
        {
            ["time"] = GameClock(),
            ["moon"] = MoonPhases.Name(Main.moonPhase)
        }, features);
    }

    // 今日任务鱼的 Net ID（Main.anglerQuestItemNetIDs[Main.anglerQuest]）。
    public static int CurrentQuestFishId
    {
        get
        {
            int[] pool = Main.anglerQuestItemNetIDs;
            int index = Main.anglerQuest;
            return pool is null || index < 0 || index >= pool.Length ? 0 : pool[index];
        }
    }

    // 当前游戏时刻，形如 HH:mm。
    public static string GameClock() => GameTime.Format(Main.time, Main.dayTime);

    // 每日播报的可替换变量。鱼的一切信息都在 {icon} 的悬停提示里，这里只给得出 ID。
    // 日报不提供旅商段落；{merchant} 仅为兼容旧模板保留为空，旅商走到货播报。
    private static Dictionary<string, string> BuildDailyContext(FeatureSwitches features)
    {
        int netId = CurrentQuestFishId;
        bool moon = features[Feature.MoonPhase];

        return new Dictionary<string, string>
        {
            ["icon"] = features[Feature.QuestFishIcon] ? $"[i:{netId}]" : string.Empty,
            ["angler"] = features[Feature.AnglerStatus] ? AnglerStatus() : string.Empty,
            ["moon"] = moon ? MoonPhases.Name(Main.moonPhase) : string.Empty,
            ["moon_bonus"] = moon ? MoonPhases.FishingBonusText(Main.moonPhase) : string.Empty,
            ["sale"] = features[Feature.MoonSale]
                ? MoonSale.Render(MoonSale.Today(Main.moonPhase, Main.remixWorld, Main.tenthAnniversaryWorld))
                : string.Empty,
            ["merchant"] = string.Empty,
            ["merchant_items"] = string.Empty,
            ["time"] = GameClock(),
            ["id"] = netId.ToString()
        };
    }

    // 按当前这场到底是雪还是沙，挑对应的模板播报。只在风暴出现的那一刻播一次。
    private void BroadcastStorm(bool blizzard, FeatureSwitches features)
    {
        if (!features[Feature.Sandstorm])
            return;

        Broadcast(blizzard ? Config.BlizzardLines : Config.SandstormLines,
            new Dictionary<string, string>
            {
                ["storm"] = blizzard ? "暴风雪" : "沙尘暴",
                ["time"] = GameClock(),
                ["moon"] = MoonPhases.Name(Main.moonPhase)
            }, features);
    }

    // 原版的沙尘暴与暴风雪是同一个 Sandstorm 事件，客户端按 player.ZoneSnow &&
    // player.ZoneRain 决定是黄沙还是暴雪。可这两个标志在专用服务器上不可靠：
    // ZoneRain = Main.raining && Y <= worldSurface 是全局的、可靠，但 ZoneSnow 来自
    // 客户端算的 Main.SceneMetrics，服务器上读到的是过期值——"一直下雨"的种子又让
    // ZoneRain 恒为真，于是 ZoneSnow && ZoneRain 退化成只看 ZoneSnow，随便一个玩家
    // 就能把沙尘暴误判成暴风雪。
    //
    // 所以不问玩家，改看地形：抽样地表明层，数雪块和沙块谁多。这个结果只跟世界本身
    // 有关，缓存一次即可。世界里两种地形都有时无法两全，按 StormType 配置由服主定夺。
    private static bool? _worldIsSnowy;

    private static bool IsBlizzard()
    {
        switch (Config.StormType)
        {
            case "sandstorm": return false;
            case "blizzard": return true;
            default: return _worldIsSnowy ??= ScanSurfaceForSnow();
        }
    }

    // 抽样扫描地表明层。沙漠地表是沙块(32)，雪原地表是雪块(51)，数这两种就够。
    private static bool ScanSurfaceForSnow()
    {
        int snow = 0, sand = 0;
        int top = (int)Main.worldSurface - 8;
        int bottom = (int)Main.worldSurface + 56;

        for (int x = 0; x < Main.maxTilesX; x += 4)
        {
            for (int y = top; y < bottom && y < Main.maxTilesY; y++)
            {
                if (!WorldGen.InWorld(x, y)) continue;

                switch (Main.tile[x, y].TileType)
                {
                    case TileID.SnowBlock: snow++; break;
                    case TileID.Sand: sand++; break;
                }
            }
        }

        PublicLogger.Info($"[TerraNews] 地形判定：雪块 {snow} 格，沙块 {sand} 格"
            + $" → 这场按{(snow > sand ? "暴风雪" : "沙尘暴")}播报。");
        return snow > sand;
    }

    // 渔夫状态。只能输出纯文本，因为外层颜色标签被剥掉后嵌套标签会变成字面文本。
    private static string AnglerStatus()
    {
        if (Main.anglerQuestFinished)
            return "状态：今日任务已有人交付，渔夫不再接受该鱼";
        if (!NPC.AnyNPCs(NPCID.Angler))
            return "状态：渔夫当前不在城镇，钓到后记得等他出现";
        return "状态：渔夫在岗，可前往接取 / 交付任务";
    }

    // 替换模板里的 {占位符}，丢掉占位符全部属于已关闭功能的那几行，
    // 清掉拼剩下的空段，再解析行首的颜色标签。
    private static ChatLine[] BuildLines(List<string> templates, Dictionary<string, string> context, FeatureSwitches features)
    {
        var result = new List<ChatLine>(templates.Count);
        foreach (string template in templates)
        {
            if (template is null || !FeatureMap.ShouldRender(template, features))
                continue;

            string text = template ?? string.Empty;
            foreach (KeyValuePair<string, string> pair in context)
                text = text.Replace("{" + pair.Key + "}", pair.Value);

            // 先剥颜色标签再清空段：反过来会把行首那个半截标签当成残段丢掉。
            ChatLine row = ChatLineParser.Parse(text);
            string tidy = ChatLineParser.TidySeparators(row.Text);
            if (tidy.Length > 0)
                result.Add(row with { Text = tidy });
        }

        return result.ToArray();
    }

    private void Broadcast(List<string> lines, Dictionary<string, string> context, FeatureSwitches features)
    {
        ChatLine[] rendered = BuildLines(lines, context, features);
        LogRendered(rendered, features);

        foreach (ChatLine row in rendered)
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(row.Text), new Color(row.R, row.G, row.B));
    }

    // 把播报镜像进日志，并把 [i:ID] 改写成 [物品#ID] —— 日志里没有客户端来渲染图标。
    private static void LogRendered(ChatLine[] rendered, FeatureSwitches features)
    {
        if (!features.ServerLog)
            return;

        var sb = new StringBuilder("[TerraNews] ");
        foreach (ChatLine row in rendered)
            sb.AppendLine("  " + ChatLineParser.ToLogText(row.Text));

        PublicLogger.Info(sb.ToString().TrimEnd());
    }
}
