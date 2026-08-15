using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

using GuZhenRen.Cards.LiDao;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(typeof(BaiZhiGu), typeof(FeiXiongZhiLiGu), typeof(ELiGu))]
[HeLianRecipe(typeof(BaiZhiGu), typeof(FeiXiongZhiLiGu), typeof(QingNiuLaoLiGu))]
[HeLianRecipe(typeof(BaiZhiGu), typeof(FeiXiongZhiLiGu), typeof(ShiGuiLiGu))]
[HeLianRecipe(typeof(BaiZhiGu), typeof(ELiGu), typeof(QingNiuLaoLiGu))]
[HeLianRecipe(typeof(BaiZhiGu), typeof(ELiGu), typeof(ShiGuiLiGu))]
[HeLianRecipe(typeof(BaiZhiGu), typeof(QingNiuLaoLiGu), typeof(ShiGuiLiGu))]
[HeLianRecipe(typeof(FeiXiongZhiLiGu), typeof(ELiGu), typeof(QingNiuLaoLiGu))]
[HeLianRecipe(typeof(FeiXiongZhiLiGu), typeof(ELiGu), typeof(ShiGuiLiGu))]
[HeLianRecipe(typeof(FeiXiongZhiLiGu), typeof(QingNiuLaoLiGu), typeof(ShiGuiLiGu))]
[HeLianRecipe(typeof(ELiGu), typeof(QingNiuLaoLiGu), typeof(ShiGuiLiGu))]
public sealed class BaiShouLiGu :
    AbstractHeLianGuCard,
    ILiDaoBeastGuCard
{
    private static readonly SavedAttachedState<CardModel, string>
        CompositionState = new(
            Entry.ModId + ".li_dao.bai_shou_composition",
            static () => string.Empty
        );

    private string _composition = string.Empty;

    public int TrainingRequired => GuRank switch
    {
        <= 2 => 3,
        <= 7 => 2,
        _ => 1,
    };

    public Type CompanionCardType => typeof(BaiShouJiaShi);
    public Type PhantomCardType => typeof(BaiShouXuYing);
    public override int RecoveryDelayTurns => GuRank >= 9 ? 4 : 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.LianLi)
            .Append(GuZhenRenKeywords.NingYing)
            .Distinct();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 25m),
        new DynamicVar("EffectPercent", 60m),
    ];

    public BaiShouLiGu() :
        base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
        RefreshRankValues();
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPhantomSystem.ActivateBaiShouGuAsync(
        choiceContext,
        this,
        GetComposition()
    );

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    ) => materials
        .OfType<IGuRankProvider>()
        .Select(provider => provider.GuRank)
        .DefaultIfEmpty(1)
        .Min();

    protected override void OnHeLianCompleted(
        IReadOnlyList<CardModel> materials
    )
    {
        LiDaoBeastKind[] kinds = materials
            .Select(LiDaoRankTable.GetBeastKind)
            .Distinct()
            .OrderBy(kind => kind)
            .ToArray();

        if (kinds.Length != 3)
        {
            throw new InvalidOperationException(
                "百兽力蛊必须由三种不同兽力蛊合练。"
            );
        }

        _composition = string.Join(',', kinds.Select(kind => (int)kind));
        CompositionState[this] = _composition;
    }

    /// <summary>
    /// 非合练来源随机获得百兽力蛊时，从五种兽力中不重复抽取三种并
    /// 固定到该卡实例。状态参与保存和多人快照，选择界面与实际催动
    /// 始终显示、使用同一组兽力虚影。
    /// </summary>
    internal void AssignRandomComposition(Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        List<LiDaoBeastKind> pool =
            [.. Enum.GetValues<LiDaoBeastKind>()];
        List<LiDaoBeastKind> selected = [];
        while (selected.Count < 3)
        {
            int index = rng.NextInt(pool.Count);
            selected.Add(pool[index]);
            pool.RemoveAt(index);
        }

        _composition = string.Join(
            ',',
            selected
                .OrderBy(kind => kind)
                .Select(kind => (int)kind)
        );
        CompositionState[this] = _composition;
    }

    internal IReadOnlyList<LiDaoBeastKind> GetComposition()
    {
        string serialized = CompositionState[this];
        if (string.IsNullOrWhiteSpace(serialized))
        {
            serialized = _composition;
        }

        LiDaoBeastKind[] parsed = serialized
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out int kind) ? kind : -1)
            .Where(kind => Enum.IsDefined(typeof(LiDaoBeastKind), kind))
            .Select(kind => (LiDaoBeastKind)kind)
            .Distinct()
            .Take(3)
            .ToArray();

        return parsed.Length == 3
            ? parsed
            :
            [
                LiDaoBeastKind.BaiZhi,
                LiDaoBeastKind.E,
                LiDaoBeastKind.QingNiu,
            ];
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Training", TrainingRequired);
        description.Add(
            "TrainingProgress",
            LiDaoTrainingSystem.GetProgress(this)
        );
    }

    public override IReadOnlyList<CardModel> GetCarouselCards()
    {
        BaiShouXuYing preview =
            GuCardReferenceFactory.Create<BaiShouXuYing>(this);
        preview.ConfigureComposition(GetComposition());
        return [preview];
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();

        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.BaiShouChance(GuRank);
        DynamicVars["EffectPercent"].BaseValue =
            LiDaoRankTable.BaiShouEffectPercent(GuRank);
    }
}
