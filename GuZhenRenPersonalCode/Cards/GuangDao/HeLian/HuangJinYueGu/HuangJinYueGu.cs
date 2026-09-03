using GuZhenRen.Cards.GuangDao;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.HeLian;

[HeLianRecipe(
    typeof(YueMangGu),
    typeof(LiuGuangGu),
    MinimumMaterialRank = 5
)]
public sealed class HuangJinYueGu
    : AbstractHeLianGuCard,
      IRefractionRelevantCard,
      IMoonlightCard
{
    private static readonly SavedAttachedState<CardModel, int>
        LastConsumedRefractionSerial = new(
            Entry.ModId + ".huang_jin_yue.last_consumed_refraction",
            static () => 0
        );

    public override int MinimumAvailableGuRank => 5;

    public override int MaxGuRank => 7;

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1, ValueProp.Move),
        new RepeatVar("BaseHits", 5),
    ];

    // 暂时复用月芒卡图；黄金月机制与代码不依赖新增二进制资源。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(YueMangGu));

    public HuangJinYueGu()
        : base(
            0,
            CardType.Attack,
            CardRarity.Rare,
            TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 2);
        SetGuRank(5);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        int moonlight = GetCurrentMoonlight();
        description.Add("Moonlight", moonlight);
        description.Add(
            "TotalHits",
            DynamicVars["BaseHits"].IntValue + moonlight
        );
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        Creature? target = cardPlay.Target;
        if (target == null || !IsValidTarget(target))
        {
            return;
        }

        int totalSerial = GuangDaoPowerSystem
            .GetTotalRefractionSerial(Owner);
        int hitCount = DynamicVars["BaseHits"].IntValue +
            GetCurrentMoonlight(totalSerial);

        // 先提交消费基线，确保目标中途死亡也不会返还月华。
        LastConsumedRefractionSerial[this] = totalSerial;

        for (int hit = 0; hit < hitCount && !target.IsDead; hit++)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override int CalculateHeLianResultRank(
        IReadOnlyList<CardModel> materials
    )
    {
        if (materials.Count != 2 ||
            materials.OfType<IGuRankProvider>()
                .Any(provider => provider.GuRank < 5))
        {
            throw new InvalidOperationException(
                "黄金月蛊需要至少五转的月芒蛊与流光蛊。"
            );
        }

        return Math.Clamp(
            materials.OfType<IGuRankProvider>()
                .Select(provider => provider.GuRank)
                .DefaultIfEmpty(5)
                .Min(),
            MinimumAvailableGuRank,
            MaxGuRank
        );
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private int GetCurrentMoonlight()
    {
        if (IsCanonical)
        {
            return 0;
        }

        return GetCurrentMoonlight(
            GuangDaoPowerSystem.GetTotalRefractionSerial(Owner)
        );
    }

    private int GetCurrentMoonlight(int totalSerial)
    {
        totalSerial = Math.Max(0, totalSerial);
        int baseline = Math.Max(0, LastConsumedRefractionSerial[this]);

        // 新战斗的折光序号从零开始；上一场的卡牌基线不得压住本场累计。
        if (baseline > totalSerial)
        {
            baseline = 0;
        }

        return totalSerial - baseline;
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = 2;
        DynamicVars["BaseHits"].BaseValue = GuRank switch
        {
            <= 5 => 5,
            6 => 6,
            _ => 7,
        };
    }
}
