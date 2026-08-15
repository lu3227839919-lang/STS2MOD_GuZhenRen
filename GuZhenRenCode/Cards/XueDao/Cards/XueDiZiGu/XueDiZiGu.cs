using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.XueDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.XueDao;

/// <summary>
/// 血滴子：攻击全体、施加血印，并在每次催动时获得一张绑定本体的
/// 血云噬身。只有该衍生牌触发血印时，才会加快来源血滴子的恢复。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class XueDiZiGu : AbstractGuWormCard
{
    private const string BreedCapVar = "BreedCap";

    public override int MaxUses => 1;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 3,
        <= 8 => 4,
        _ => 5,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new PowerVar<XueYinPower>(1m),
        new DynamicVar(BreedCapVar, 1m),
    ];

    // 使用 images/cards/XueDiZiGu.png 同名卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(XueDiZiGu));

    public XueDiZiGu()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        SetDao(Dao.XueDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay
    )
    {
        if (CombatState == null)
        {
            return;
        }

        Creature[] targets = GuZhenRenDeterminism
            .OrderCreatures(CombatState.HittableEnemies)
            .Where(static target => target.IsAlive)
            .ToArray();

        await DamageCmd
            .Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_bloody_impact")
            .SpawningHitVfxOnEachCreature()
            .Execute(choiceContext);

        int marks = DynamicVars[typeof(XueYinPower).Name].IntValue +
            (targets.Length == 1 ? 1 : 0);
        foreach (Creature target in targets.Where(static c => c.IsAlive))
        {
            await XueDaoPowerSystem.ApplyXueYin(
                choiceContext,
                this,
                target,
                marks
            );
        }

        XueYunShiShen derived =
            GuGeneratedCardFactory.Create<XueYunShiShen>(
                Owner,
                GuRank,
                upgraded: false
            );
        derived.BindSource(this);
        await GuGeneratedCardFactory.AddToHandOrDiscard(
            derived,
            Owner
        );
    }

    public override IReadOnlyList<CardModel> GetCarouselCards() =>
    [
        GuCardReferenceFactory.Create<XueYunShiShen>(this, false),
    ];

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 3,
            2 => 4,
            3 => 5,
            4 => 6,
            5 => 7,
            6 => 9,
            7 => 11,
            8 => 13,
            _ => 16,
        };
        DynamicVars[typeof(XueYinPower).Name].BaseValue = GuRank switch
        {
            <= 3 => 1,
            <= 6 => 2,
            <= 8 => 3,
            _ => 4,
        };
        DynamicVars[BreedCapVar].BaseValue = GuRank switch
        {
            <= 5 => 1,
            <= 8 => 2,
            _ => 3,
        };
    }

    protected override void OnUpgrade()
    {
    }
}

/// <summary>
/// 血滴子的战斗衍生牌。非消耗，正常进入弃牌堆并可在本场战斗中循环。
/// </summary>

