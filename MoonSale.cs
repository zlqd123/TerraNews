using System;
using System.Collections.Generic;
using System.Linq;
namespace TerraNews;

using Terraria.ID;

// 出现在日报里的店铺，顺序照城镇 NPC 的解锁表排，渔夫由任务鱼另行处理，
// 骷髅商人排在最后——它是 1.4.4 才加的新 NPC，不在那张表里。
internal enum Shop
{
    Merchant,      // 商人
    DyeTrader,     // 染料商
    Zoologist,     // 动物学家
    Dryad,         // 树妖
    Painter,       // 油漆工
    Hairstylist,   // 发型师
    Clothier,      // 服装商
    Mechanic,      // 机械师
    Steampunker,   // 蒸汽朋克人
    Cyborg,        // 机器侠
    Princess,      // 公主
    Skeleton       // 骷髅商人
}

// 一件货：物品 ID，外加一个跟在图标后面的限定说明，写成「（非墓地）」这样的样子。
internal readonly record struct Lot(int Item, string? Note = null);

// 硬编码的「随月相 / 天气轮换的货架」对照表。
//
// 数据是从 Chest.SetupShop 里逐条挑出来的：只抄判断式里带 Main.moonPhase、
// Main.eclipse、Main.bloodMoon、Main.IsItAHappyWindyDay 的分支，昼夜、血月强度、
// Boss 进度和「看你背包」这类条件一律不抄。所以这里跑一次就知道当天该播什么，
// 不必去调原版的 SetupShop。
//
// 这张表对应 SourceGameVersion 标的游戏版本，换版本后请重新核一遍。
internal static class MoonSale
{
    // 表的来源版本。启动时和 Main.versionNumber 比一下，对不上就打一行警告，但不拦播报。
    public const string SourceGameVersion = "Terraria 1.4.4.9 / tModLoader v2026.8.3.0（1.4.4 分支）";
    public const string SourceVersionNumber = "1.4.4.9";

    // 世界种子在物品后面的标注，用原版的字段短码而不是种子名字。
    public const string RemixNote = "（remix）";   // Main.remixWorld，即 Don't Dig Up
    public const string TenthNote = "（tenth）";   // Main.tenthAnniversaryWorld
    public const string NoGraveyardNote = "（非墓地）";
    public const string NightNote = "（夜晚）";  // Chest.cs 里条件带 !Main.dayTime
    public const string DayNote = "（白天）";

    private static readonly string[] ShopNames =
    {
        "商人", "染料商", "动物学家", "树妖", "油漆工", "发型师",
        "服装商", "机械师", "蒸汽朋克人", "机器侠", "公主", "骷髅商人"
    };

    // 下弦月的火花魔棒会被重混世界的魔法飞刀顶掉，这里记下替换关系。
    private const int SparkingWand = 3069;
    private const int MagicDagger = 517;

    // 今日月相对应的轮换货。键是 Main.moonPhase，0 满月 / 1 亏凸 / 2 下弦 / 3 残月 /
    // 4 新月 / 5 娥眉 / 6 上弦 / 7 盈凸。数组内部已按店铺顺序排好，直接拼即可。
    //
    // 夜间限售的也收进来，但只收「条件里除了夜晚还带别的判断」的那些——满月的魔法灯笼、
    // 狼人、白色燕尾服都是满月+夜晚两个条件。军火商、商人、巫医、骷髅商人夜间那批
    // 在 Chest.cs 里就是一句光秃秃的 if (!Main.dayTime)，每天晚上都一样，不进表。
    //
    // 这张表是按 1.4.4.9 的 Chest.SetupShop 逐条抄的，和 1.4.5.8 那版有这么几处不同：
    //   1) 树妖盆栽在 1.4.4.9 只有 if (Main.hardMode) 分支，后面直接 break、没有 else，
    //      所以困难模式之前树妖一件月相货都不卖，整项不收。1.4.5.8 才补了非困难模式分支。
    //   2) 5640/5641/5642 这三件 1.4.4.9 里根本不存在，是 1.4.5 加的（就是绿/经典/派对
    //      三双溜冰鞋 RollerSkatesGreen/Classic/PartyMountItem），已删。
    //   3) 爆破专家夜里的 5542 也是 1.4.5 才有的，本表本来就没收。
    //   4) 1.4.5 给血月的树妖加了草地回响（4504/4508）、给魔法师加了杀戮塔卡组
    //      （5461，硬模式限），都是 1.4.4.9 里不存在的物品，没收。
    // 以下为 2026-11 对照 1.4.0.5/1.4.5.8 原版源码补上的缺口（1.4.4.9 里都有）：
    //   - 骷髅商人下弦/残月/上弦/盈凸四相的木箭（40）：骨箭只在满月/亏凸/新月/娥眉四相。
    //   - 血月树妖的腐化/猩红种子（59/2171）。
    //   - 血月机器侠的 RocketIII（773）：原版条件「夜间或日食」，血月必在夜间。
    //   - 蒸汽朋克人日食/血月的溶液按世界分流：猩红 RedSolution / 腐化 PurpleSolution(782)。
    private static readonly (Shop Shop, Lot[] Lots)[][] Daily =
    {
        // 0 满月 —— 这一天所有只在夜里上架的东西全在：魔法灯笼、狼人、白色燕尾服
        new[]
        {
            (Shop.DyeTrader, new Lot[] { new(ItemID.ShadowDye), new(ItemID.NegativeDye) }),
            (Shop.Zoologist, new Lot[] { new(ItemID.DogEars), new(ItemID.DogTail),
                new(ItemID.TheWerewolf, NightNote) }),
            (Shop.Painter, new Lot[] { new(ItemID.FirstEncounter, NoGraveyardNote) }),
            (Shop.Clothier, new Lot[] { new(ItemID.PlumbersShirt), new(ItemID.PlumbersPants),
                new(ItemID.WhiteTuxedoShirt, NightNote), new(ItemID.WhiteTuxedoPants, NightNote) }),
            (Shop.Skeleton, new Lot[] { new(ItemID.WoodenBoomerang), new(ItemID.StrangeBrew),
                new(ItemID.SpelunkerGlowstick), new(ItemID.BoneArrow), new(ItemID.BlueCounterweight),
                new(ItemID.MagicLantern, NightNote) })
        },
        // 1 亏凸月
        new[]
        {
            (Shop.Zoologist, new Lot[] { new(ItemID.DogEars), new(ItemID.DogTail) }),
            (Shop.Painter, new Lot[] { new(ItemID.FirstEncounter, NoGraveyardNote) }),
            (Shop.Clothier, new Lot[] { new(ItemID.TheDoctorsShirt), new(ItemID.TheDoctorsPants) }),
            (Shop.Mechanic, new Lot[] { new(ItemID.MechanicsRod) }),
            (Shop.Skeleton, new Lot[] { new(ItemID.Umbrella), new(ItemID.LesserHealingPotion),
                new(ItemID.BoneArrow), new(ItemID.RedCounterweight) })
        },
        // 2 下弦月 —— 火花魔棒 / 魔法飞刀按种子二选一
        new[]
        {
            (Shop.Zoologist, new Lot[] { new(ItemID.FoxEars), new(ItemID.FoxTail) }),
            (Shop.Painter, new Lot[] { new(ItemID.GoodMorning, NoGraveyardNote) }),
            (Shop.Hairstylist, new Lot[] { new(ItemID.TimeHairDye, DayNote) }),
            (Shop.Mechanic, new Lot[] { new(ItemID.MechanicsRod) }),
            (Shop.Skeleton, new Lot[] { new(SparkingWand), new(ItemID.StrangeBrew),
                new(ItemID.WoodenArrow), new(ItemID.PurpleCounterweight) })
        },
        // 3 残月
        new[]
        {
            (Shop.Zoologist, new Lot[] { new(ItemID.FoxEars), new(ItemID.FoxTail) }),
            (Shop.Painter, new Lot[] { new(ItemID.GoodMorning, NoGraveyardNote) }),
            (Shop.Mechanic, new Lot[] { new(ItemID.MechanicsRod) }),
            (Shop.Skeleton, new Lot[] { new(ItemID.PortableStool), new(ItemID.LesserHealingPotion),
                new(ItemID.WoodenArrow), new(ItemID.GreenCounterweight), new(ItemID.ArtisanLoaf) })
        },
        // 4 新月
        new[]
        {
            (Shop.Zoologist, new Lot[] { new(ItemID.LizardEars), new(ItemID.LizardTail) }),
            (Shop.Painter, new Lot[] { new(ItemID.UndergroundReward, NoGraveyardNote) }),
            (Shop.Skeleton, new Lot[] { new(ItemID.Aglet), new(ItemID.StrangeBrew), new(ItemID.BoneArrow),
                new(ItemID.BlueCounterweight), new(ItemID.ArtisanLoaf) })
        },
        // 5 娥眉月
        new[]
        {
            (Shop.Zoologist, new Lot[] { new(ItemID.LizardEars), new(ItemID.LizardTail) }),
            (Shop.Painter, new Lot[] { new(ItemID.UndergroundReward, NoGraveyardNote) }),
            (Shop.Mechanic, new Lot[] { new(ItemID.MechanicsRod) }),
            (Shop.Skeleton, new Lot[] { new(ItemID.ClimbingClaws), new(ItemID.LesserHealingPotion),
                new(ItemID.BoneArrow), new(ItemID.RedCounterweight), new(ItemID.ArtisanLoaf) })
        },
        // 6 上弦月
        new[]
        {
            (Shop.Zoologist, new Lot[] { new(ItemID.BunnyEars), new(ItemID.BunnyTail) }),
            (Shop.Painter, new Lot[] { new(ItemID.ThroughtheWindow, NoGraveyardNote) }),
            (Shop.Hairstylist, new Lot[] { new(ItemID.TimeHairDye, DayNote) }),
            (Shop.Skeleton, new Lot[] { new(ItemID.CordageGuide), new(ItemID.StrangeBrew),
                new(ItemID.WoodenArrow), new(ItemID.PurpleCounterweight) })
        },
        // 7 盈凸月
        new[]
        {
            (Shop.Zoologist, new Lot[] { new(ItemID.BunnyEars), new(ItemID.BunnyTail) }),
            (Shop.Painter, new Lot[] { new(ItemID.ThroughtheWindow, NoGraveyardNote) }),
            (Shop.Mechanic, new Lot[] { new(ItemID.MechanicsRod) }),
            (Shop.Skeleton, new Lot[] { new(ItemID.Radar), new(ItemID.LesserHealingPotion),
                new(ItemID.WoodenArrow), new(ItemID.GreenCounterweight) })
        }
    };

    // 公主的四张卡同样按月相两两轮换，但要十周年世界才上架，货后面标种子码。
    private static readonly (int[] Phases, int Item)[] PrincessCards =
    {
        (new[] { 0, 1 }, ItemID.PirateStaff),
        (new[] { 2, 3 }, ItemID.DiscountCard),
        (new[] { 4, 5 }, ItemID.LuckyCoin),
        (new[] { 6, 7 }, ItemID.CoinGun)
    };

    // 大风天（Main.IsItAHappyWindyDay 且在白天）只有商人多卖一把风车。
    private static readonly (Shop Shop, Lot[] Lots)[] Windy =
    {
        (Shop.Merchant, new Lot[] { new(ItemID.PinWheel) })
    };

    // 日食。机器侠三件、蒸汽朋克人一件。
    // 蒸汽朋克人的溶液跟世界类型走：原版判据是 eclipse || bloodMoon 时按
    // WorldGen.crimson 给 RedSolution / PurpleSolution，日食与血月同一套，
    // 所以不放在静态表里，由 EclipseSale(crimson) 现拼。
    private static readonly (Shop Shop, Lot[] Lots)[] Eclipse =
    {
        (Shop.Cyborg, new Lot[] { new(ItemID.RocketIII), new(774), new(ItemID.ClusterRocketII) })
    };

    // 血月的不分世界部分：丑角面具之外，服装商还多卖一身堕落燕尾服。
    // 那两件在 Chest.cs 里带 !Main.dayTime，可血月本来就只发生在夜里，所以不必再标「夜晚」。
    // 树妖的毒粉/种子、蒸汽朋克人的溶液、机器侠的 RocketIII 都按世界或昼夜条件现拼，
    // 见 BloodMoonSale(crimson)。
    private static readonly (Shop Shop, Lot[] Lots)[] BloodMoon =
    {
        (Shop.Merchant, new Lot[] { new(ItemID.ThrowingKnife) }),
        (Shop.DyeTrader, new Lot[] { new(ItemID.BloodbathDye) }),
        (Shop.Painter, new Lot[] { new(ItemID.EvilPresence) }),
        (Shop.Clothier, new Lot[] { new(ItemID.MimeMask),
            new(ItemID.FallenTuxedoShirt), new(ItemID.FallenTuxedoPants) })
    };

    // 这张表是纯函数，不碰任何 Terraria 静态字段：月相与三个世界标志都由调用方传进来。
    // 这样既能在这里单测，也不会因为 Main 的静态初始化在测试环境里炸掉。
    public static (Shop Shop, Lot[] Lots)[] Today(int moonPhase, bool remix, bool tenthAnniversary)
    {
        int phase = ((moonPhase % 8) + 8) % 8;
        var rows = new List<(Shop Shop, Lot[] Lots)>(Daily[phase]);

        if (remix)
            SwapWandForDagger(rows);

        if (tenthAnniversary)
        {
            foreach ((int[] phases, int item) in PrincessCards)
                if (Array.IndexOf(phases, phase) >= 0)
                    rows.Add((Shop.Princess, new Lot[] { new(item, TenthNote) }));
        }

        return rows.ToArray();
    }

    // 重混世界把下弦月的火花魔棒换成魔法飞刀，其余不动。
    // 必须先复制一份再改：Daily 里的数组是静态共享的，直接改会把普通世界的表也污染掉。
    private static void SwapWandForDagger(List<(Shop Shop, Lot[] Lots)> rows)
    {
        for (int r = 0; r < rows.Count; r++)
        {
            if (rows[r].Shop != Shop.Skeleton)
                continue;

            var source = rows[r].Lots;
            var lots = new Lot[source.Length];
            for (int i = 0; i < source.Length; i++)
                lots[i] = source[i].Item == SparkingWand
                    ? source[i] with { Item = MagicDagger, Note = RemixNote }
                    : source[i];

            rows[r] = (rows[r].Shop, lots);
        }
    }

    public static (Shop Shop, Lot[] Lots)[] WindyDay() => Windy;

    // 日食货架。溶液按世界选：猩红世界 RedSolution、腐化世界 PurpleSolution。
    public static (Shop Shop, Lot[] Lots)[] EclipseSale(bool crimson)
    {
        var rows = new List<(Shop Shop, Lot[] Lots)>(Eclipse);
        rows.Insert(0, (Shop.Steampunker, new Lot[]
        {
            new(crimson ? ItemID.RedSolution : ItemID.PurpleSolution)
        }));
        return rows.ToArray();
    }

    // 血月货架。三处按世界/昼夜现拼：
    //   - 树妖：毒粉分猩红 2886 / 腐化 67（两者同名，不在播报里标世界）；
    //     血月还卖腐化/猩红种子，1.4.0.5 起就有，同样跟世界走。
    //   - 蒸汽朋克人：溶液与日食同一套判据，RedSolution / PurpleSolution。
    //   - 机器侠：RocketIII 的条件是「夜间或日食」，血月必然在夜里，所以血月也卖，
    //     排在 RocketII 之后、ClusterRocketII 之前，照原版货位。
    public static (Shop Shop, Lot[] Lots)[] BloodMoonSale(bool crimson)
    {
        var rows = new List<(Shop Shop, Lot[] Lots)>(BloodMoon);
        rows.Insert(2, (Shop.Dryad, new Lot[]
        {
            new(crimson ? ItemID.ViciousPowder : ItemID.VilePowder),
            new(crimson ? ItemID.CrimsonSeeds : ItemID.CorruptSeeds)
        }));
        rows.Add((Shop.Steampunker, new Lot[]
        {
            new(crimson ? ItemID.RedSolution : ItemID.PurpleSolution)
        }));
        rows.Add((Shop.Cyborg, new Lot[]
        {
            new(ItemID.RocketII), new(ItemID.RocketIII), new(ItemID.ClusterRocketII)
        }));
        return rows.ToArray();
    }

    public static string ShopName(Shop shop) => ShopNames[(int)shop];

    // 拼成「骷髅商人：[i:284][i:3001]；服装商：[i:245][i:246]」，图标之间不留空格。
    public static string Render(IEnumerable<(Shop Shop, Lot[] Lots)> rows)
    {
        var parts = new List<string>();
        foreach ((Shop shop, Lot[] lots) in rows)
        {
            if (lots.Length == 0)
                continue;

            var sb = new System.Text.StringBuilder(ShopName(shop)).Append('：');
            foreach (Lot lot in lots)
            {
                sb.Append("[i:").Append(lot.Item).Append(']');
                if (!string.IsNullOrEmpty(lot.Note))
                    sb.Append(lot.Note);
            }

            parts.Add(sb.ToString());
        }

        return string.Join("；", parts);
    }

    // 版本对不上时返回一句话，正常返回 null。
    public static string? VersionWarning(string currentVersion)
    {
        if (currentVersion.StartsWith("v" + SourceVersionNumber, StringComparison.Ordinal))
            return null;

        return $"[TerraNews] 当前游戏版本 {currentVersion}，货架对照表按 {SourceGameVersion} 抄的。"
            + "版本不符时播报可能与实际货架对不上。";
    }
}
