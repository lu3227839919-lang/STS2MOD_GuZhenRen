// ============================================================================
// 中文维护说明
// 文件职责：实现灾劫系统的领域模型、注册表、平衡配置与生成流程。
// 主要类型：TribulationIds。
// 实现要点：公开成员构成该模块的稳定协作面；修改签名时应同步检查注册点与调用方。
// 维护约定：灾劫选择先持久化再应用；新增钩子时同步更新事件路由和幂等标记。
// ============================================================================
namespace GuZhenRen.Tribulations.Core;

public static class TribulationIds
{
    public const string XuanBaiFeiYan = "earth/xuan_bai_fei_yan";
    public const string NiZhaoXie = "earth/ni_zhao_xie";
    public const string LiuXingHuoYu = "earth/liu_xing_huo_yu";
    public const string RongDi = "earth/rong_di";
    public const string FuShiAnLiu = "earth/fu_shi_an_liu";
    public const string YinYunBaiHai = "earth/yin_yun_bai_hai";
    public const string XueGuai = "earth/xue_guai";
    public const string MeiLanDianYing = "earth/mei_lan_dian_ying";
    public const string JiBanLangYan = "earth/ji_ban_lang_yan";
    public const string HeiYanXingZhui = "earth/hei_yan_xing_zhui";
    public const string ChunXiaoCuiLi = "earth/chun_xiao_cui_li";
    public const string FengHua = "earth/feng_hua";
    public const string XueYue = "earth/xue_yue";
}
