using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace TerraNews;

// 世界事件的上升沿检测，只在事件出现的那一刻报一次，不跟进强度变化。
// 草药提醒也走这里：满月夜/血月/下雨都是「出现的那一刻播一次」的上升沿事件，
// 火焰花是每天一次的定点提醒，去重在 TerraNewsSystem 里做。
public sealed class WorldEventWatcher
{
    private bool _stormWasHappening;
    private bool _windyWasOn;
    private bool _eclipseWasOn;
    private bool _bloodMoonWasOn;
    private bool _rainingWasOn;
    private bool _fullMoonNightWasOn;

    // 喂进一个服务器刻的风暴状态，在它开始的那一刻返回 true。
    public bool TickSandstorm(bool happening)
    {
        bool started = happening && !_stormWasHappening;
        _stormWasHappening = happening;
        return started;
    }

    // 大风天每天黎明掷一次，只在白天有货，所以取「白天且有风」。
    public bool TickWindy(bool windyDaytime)
    {
        bool started = windyDaytime && !_windyWasOn;
        _windyWasOn = windyDaytime;
        return started;
    }

    public bool TickEclipse(bool on)
    {
        bool started = on && !_eclipseWasOn;
        _eclipseWasOn = on;
        return started;
    }

    public bool TickBloodMoon(bool on)
    {
        bool started = on && !_bloodMoonWasOn;
        _bloodMoonWasOn = on;
        return started;
    }

    // 开始下雨的那一刻。水叶草只在雨里开花，原版判据是 Main.raining ||
    // Main.cloudAlpha > 0（后者是雨刚停云没散完的尾巴），这里跟着用同一套。
    public bool TickRaining(bool raining)
    {
        bool started = raining && !_rainingWasOn;
        _rainingWasOn = raining;
        return started;
    }

    // 满月夜（夜间、月相 0、非血月）的开始时刻。血月夜不算：那一晚只发血月
    // 那条死亡草提醒，见 TerraNewsSystem 里的同晚去重。
    public bool TickFullMoonNight(bool on)
    {
        bool started = on && !_fullMoonNightWasOn;
        _fullMoonNightWasOn = on;
        return started;
    }

    // 旅商货架去重后的商品；Main.travelShop 是 40 格定长数组，0 表示空位。
    public static List<int> MerchantStock(int[]? travelShop)
    {
        var items = new List<int>();
        if (travelShop is null)
            return items;

        foreach (int id in travelShop)
            if (id > 0 && !items.Contains(id))
                items.Add(id);

        return items;
    }

    // 把货架渲染成只有可悬停图标、没有名称的一行。
    public static string MerchantIconRow(IReadOnlyList<int> stock)
    {
        if (stock.Count == 0)
            return "（今日货架是空的）";

        return string.Join(string.Empty, stock.Select(id => "[i:" + id + "]"));
    }

    // 旅商那一段，排在日报最末，格式和其他店一样。货架是空的就不产出这一段，
    // 免得日报尾部留下一个光秃秃的「旅商：」。Main.travelShop 由
    // Chest.SetupTravelShop() 在刷出旅商之前掷好，所以人不在场时读到的是她上次的货。
    public static string MerchantRow(int[]? travelShop)
    {
        var stock = MerchantStock(travelShop);
        return stock.Count == 0 ? string.Empty : "旅商：" + MerchantIconRow(stock);
    }
}
