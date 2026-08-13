using GuZhenRen.Cards.HeLian;
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

namespace GuZhenRen.Cards.LiDao;

public abstract class AbstractLiDaoGuCard :
    AbstractGuWormCard,
    ILiDaoTrainingGuCard
{
    public abstract int TrainingRequired { get; }

    public abstract Type CompanionCardType { get; }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.LianLi)
            .Distinct();

    protected AbstractLiDaoGuCard(
        CardRarity rarity
    ) : base(0, CardType.Skill, rarity, TargetType.Self)
    {
        SetDao(Dao.LiDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, YuanQiCost);
    }

    protected override void AddExtraArgsToDescription(
        LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Training", TrainingRequired);
        description.Add(
            "TrainingProgress",
            LiDaoTrainingSystem.GetProgress(this)
        );
    }

    /// <summary>
    /// 蛊升转/读档/复制后同步永久牌组中的对应伴生牌转数，
    /// 让伴生牌卡面显示的转数始终与对应力道蛊一致。
    /// 子类覆写时继续调用 base.OnGuRankChanged() 即可。
    /// </summary>
    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        LiDaoCompanionSystem.SyncCompanionsForGu(this);
    }

    public override Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy
    ) => ReferenceEquals(card, this) &&
         Pile?.Type == PileType.Deck &&
         oldPileType != PileType.Deck
            ? LiDaoCompanionSystem.EnsureForGuAsync(this)
            : Task.CompletedTask;

    public override Task BeforeCardRemoved(CardModel card) =>
        ReferenceEquals(card, this)
            ? LiDaoCompanionSystem.RemoveOneForGuAsync(this)
            : Task.CompletedTask;
}

public abstract class AbstractLiDaoBeastGuCard<TPhantom> :
    AbstractLiDaoGuCard,
    ILiDaoBeastGuCard
    where TPhantom : AbstractLiDaoXuYing
{
    public Type PhantomCardType => typeof(TPhantom);

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.NingYing)
            .Distinct();

    protected AbstractLiDaoBeastGuCard(CardRarity rarity) : base(rarity)
    {
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPhantomSystem.ActivateBeastGuAsync<TPhantom>(
        choiceContext,
        this
    );

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
        [GuCardReferenceFactory.Create<TPhantom>(this)];
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class BaiZhiGu :
    AbstractLiDaoBeastGuCard<BaiZhiXuYing>
{
    public override int TrainingRequired => GuRank >= 5 ? 1 : 2;
    public override Type CompanionCardType => typeof(ChenJianChong);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 30m),
        new DamageVar(5m, ValueProp.Move),
    ];

    public BaiZhiGu() : base(CardRarity.Common) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.BaiZhiChance(GuRank);
        DynamicVars.Damage.BaseValue =
            LiDaoRankTable.BaiZhiDamage(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class FeiXiongZhiLiGu :
    AbstractLiDaoBeastGuCard<FeiXiongXuYing>
{
    public override int TrainingRequired => GuRank switch
    {
        <= 2 => 3,
        <= 6 => 2,
        _ => 1,
    };

    public override Type CompanionCardType => typeof(FeiXiongZhuang);
    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 4 => 2,
        <= 8 => 3,
        _ => 4,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 18m),
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar("DivineMight", 0m),
    ];

    public FeiXiongZhiLiGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.FeiXiongChance(GuRank);
        DynamicVars.Damage.BaseValue =
            LiDaoRankTable.FeiXiongDamage(GuRank);
        DynamicVars["DivineMight"].BaseValue =
            LiDaoRankTable.FeiXiongDivineMight(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class ELiGu : AbstractLiDaoBeastGuCard<EXuYing>
{
    public override int TrainingRequired => GuRank >= 6 ? 1 : 2;
    public override Type CompanionCardType => typeof(JiaoShuai);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 25m),
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Hits", 2m),
    ];

    public ELiGu() : base(CardRarity.Uncommon) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.EChance(GuRank);
        DynamicVars.Damage.BaseValue = LiDaoRankTable.EDamage(GuRank);
        DynamicVars["Hits"].BaseValue = LiDaoRankTable.EHits(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class QingNiuLaoLiGu :
    AbstractLiDaoBeastGuCard<QingNiuXuYing>
{
    public override int TrainingRequired => GuRank >= 5 ? 1 : 2;
    public override Type CompanionCardType => typeof(NiuJiaoDing);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 35m),
        new DamageVar(4m, ValueProp.Move),
        new BlockVar(2m, ValueProp.Move),
    ];

    public QingNiuLaoLiGu() : base(CardRarity.Common) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.QingNiuChance(GuRank);
        DynamicVars.Damage.BaseValue =
            LiDaoRankTable.QingNiuDamage(GuRank);
        DynamicVars.Block.BaseValue =
            LiDaoRankTable.QingNiuBlock(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class ShiGuiLiGu :
    AbstractLiDaoBeastGuCard<ShiGuiXuYing>
{
    public override int TrainingRequired => GuRank >= 6 ? 1 : 2;
    public override Type CompanionCardType => typeof(ChenZhuang);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chance", 30m),
        new BlockVar(4m, ValueProp.Move),
    ];

    public ShiGuiLiGu() : base(CardRarity.Uncommon) => RefreshRankValues();

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars["Chance"].BaseValue =
            LiDaoRankTable.ShiGuiChance(GuRank);
        DynamicVars.Block.BaseValue =
            LiDaoRankTable.ShiGuiBlock(GuRank);
    }
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class KuLiGu : AbstractLiDaoGuCard
{
    public override int TrainingRequired => GuRank >= 5 ? 1 : 2;
    public override Type CompanionCardType => typeof(KuLian);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DynamicVar("ChancePerHardship", 2m)];

    public KuLiGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateKuLiAsync(
        choiceContext,
        this
    );

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues() =>
        DynamicVars["ChancePerHardship"].BaseValue = GuRank switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            <= 5 => 5,
            6 => 6,
            7 => 7,
            8 => 8,
            _ => 10,
        };
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class ZiLiGengShengGu : AbstractLiDaoGuCard
{
    public override int TrainingRequired => GuRank switch
    {
        <= 2 => 3,
        <= 5 => 2,
        _ => 1,
    };

    public override Type CompanionCardType => typeof(TiaoXiYunLi);
    public override int RecoveryDelayTurns => LiDaoRankTable.Recovery(GuRank);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new HealVar(1m)];

    public ZiLiGengShengGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPowerSystem.ActivateZiLiAsync(
        choiceContext,
        this
    );

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues() => DynamicVars.Heal.BaseValue =
        LiDaoRankTable.ZiLiHealingCap(GuRank);
}

[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class QuanLiYiFuGu : AbstractLiDaoGuCard
{
    public override int YuanQiCost => 2;

    public override int TrainingRequired => GuRank switch
    {
        <= 3 => 3,
        <= 7 => 2,
        _ => 1,
    };

    public override Type CompanionCardType => typeof(YunLi);
    public override int RecoveryDelayTurns => GuRank >= 9 ? 4 : 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DynamicVar("EffectPercent", 100m)];

    public QuanLiYiFuGu() : base(CardRarity.Rare) => RefreshRankValues();

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    ) => LiDaoPhantomSystem.ActivateFullForceGuAsync(
        choiceContext,
        this
    );

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
        [GuCardReferenceFactory.Create<QuanLiXuYing>(this)];

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues() =>
        DynamicVars["EffectPercent"].BaseValue =
            LiDaoRankTable.FullForcePercent(GuRank);
}

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

    public override Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy
    ) => ReferenceEquals(card, this) &&
         Pile?.Type == PileType.Deck &&
         oldPileType != PileType.Deck
            ? LiDaoCompanionSystem.EnsureForGuAsync(this)
            : Task.CompletedTask;

    public override Task BeforeCardRemoved(CardModel card) =>
        ReferenceEquals(card, this)
            ? LiDaoCompanionSystem.RemoveOneForGuAsync(this)
            : Task.CompletedTask;

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
