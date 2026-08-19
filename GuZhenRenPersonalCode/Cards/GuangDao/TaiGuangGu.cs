using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Multiplayer;
using GuZhenRen.Powers.GuangDao;

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

namespace GuZhenRen.Cards.GuangDao;

/// <summary>
/// 太光蛊：三次催动的群攻蛊。耀化后本次伤害无视格挡，并提高照破。
/// </summary>
[RegisterCard(typeof(GuZhenRenGuCardPool))]
public sealed class TaiGuangGu : AbstractGuWormCard
{
    private const int GuangHuiCost = 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords
            .Append(GuZhenRenKeywords.GetYaoHuaKeyword(3))
            .Distinct();

    public override int MaxUses => 3;

    public override int RecoveryDelayTurns => GuRank switch
    {
        <= 5 => 4,
        <= 8 => 5,
        _ => 6,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new PowerVar<ZhaoPoPower>(1m),
    ];

    // 使用 images/cards/TaiGuangGu.png 同名卡图。
    public override CardAssetProfile AssetProfile =>
        CardImageCatalog.Create(typeof(TaiGuangGu));

    public TaiGuangGu()
        : base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "EmpoweredZhaoPo",
            DynamicVars[typeof(ZhaoPoPower).Name].IntValue + 1
        );
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

        bool empowered = await GuangDaoPowerSystem.TryAutoSpendGuangHui(
            choiceContext,
            this,
            cardPlay,
            GuangHuiCost
        );

        Creature[] targets = GuZhenRenDeterminism
            .OrderCreatures(CombatState.HittableEnemies)
            .Where(static target => target.IsAlive)
            .ToArray();

        if (empowered)
        {
            await CreatureCmd.Damage(
                choiceContext,
                targets,
                DynamicVars.Damage.BaseValue,
                ValueProp.Move | ValueProp.Unblockable,
                Owner.Creature,
                this,
                cardPlay
            );
        }
        else
        {
            await DamageCmd
                .Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_starry_impact")
                .SpawningHitVfxOnEachCreature()
                .Execute(choiceContext);
        }

        int zhaoPo = DynamicVars[typeof(ZhaoPoPower).Name].IntValue +
            (empowered ? 1 : 0);
        foreach (Creature target in targets.Where(static c => c.IsAlive))
        {
            await GuangDaoPowerSystem.ApplyZhaoPo(
                choiceContext,
                this,
                target,
                zhaoPo
            );
        }
    }

    protected override void OnGuRankChanged()
    {
        base.OnGuRankChanged();
        RefreshRankValues();
    }

    private void RefreshRankValues()
    {
        DynamicVars.Damage.BaseValue = GuRank switch
        {
            <= 1 => 5,
            2 => 6,
            3 => 7,
            4 => 8,
            5 => 10,
            6 => 12,
            7 => 14,
            8 => 17,
            _ => 21,
        };
        DynamicVars[typeof(ZhaoPoPower).Name].BaseValue =
            GuRank >= 9 ? 2 : 1;
    }

    protected override void OnUpgrade()
    {
    }
}
