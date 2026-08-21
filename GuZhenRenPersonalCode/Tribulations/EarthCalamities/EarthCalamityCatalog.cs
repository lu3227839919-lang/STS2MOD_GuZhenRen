// ============================================================================
// 中文维护说明
// 文件职责：提供地灾目录、共享基类或灾害辅助逻辑。
// 主要类型：EarthCalamityCatalog。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
using GuZhenRen.Tribulations.Core;
using GuZhenRen.Tribulations.EarthCalamities.ChunXiaoCuiLi;
using GuZhenRen.Tribulations.EarthCalamities.FengHua;
using GuZhenRen.Tribulations.EarthCalamities.FuShiAnLiu;
using GuZhenRen.Tribulations.EarthCalamities.HeiYanXingZhui;
using GuZhenRen.Tribulations.EarthCalamities.LiuXingHuoYu;
using GuZhenRen.Tribulations.EarthCalamities.MeiLanDianYing;
using GuZhenRen.Tribulations.EarthCalamities.NiZhaoXie;
using GuZhenRen.Tribulations.EarthCalamities.RongDi;
using GuZhenRen.Tribulations.EarthCalamities.XuanBaiFeiYan;
using GuZhenRen.Tribulations.EarthCalamities.XueGuai;
using GuZhenRen.Tribulations.EarthCalamities.XueYue;
using GuZhenRen.Tribulations.EarthCalamities.YinYunBaiHai;

namespace GuZhenRen.Tribulations.EarthCalamities;

public static class EarthCalamityCatalog
{
    public static void RegisterAll(TribulationRegistry registry)
    {
        registry.Register(new XuanBaiFeiYanEarthCalamity());
        registry.Register(new NiZhaoXieEarthCalamity());
        registry.Register(new RongDiEarthCalamity());
        registry.Register(new FengHuaEarthCalamity());
        registry.Register(new XueGuaiEarthCalamity());
        registry.Register(new LiuXingHuoYuEarthCalamity());
        registry.Register(new FuShiAnLiuEarthCalamity());
        registry.Register(new YinYunBaiHaiEarthCalamity());
        registry.Register(new MeiLanDianYingEarthCalamity());
        registry.Register(new XueYueEarthCalamity());
        registry.Register(new ChunXiaoCuiLiEarthCalamity());
        registry.Register(new HeiYanXingZhuiEarthCalamity());
    }
}
