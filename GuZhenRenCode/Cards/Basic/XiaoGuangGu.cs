using GuZhenRen.Characters;
using GuZhenRen.Combat;
using GuZhenRen.Powers.GuangDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Cards.GuangDao;

[RegisterCard(typeof(GuZhenRenGuCardPool))]
[RegisterCharacterStarterCard(typeof(GuZhenRenCharacter), 3)]
public sealed class XiaoGuangGu : AbstractGuWormCard
{
    public override int MaxUses => IsUpgraded ? 2 : 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m),
    ];

    public override CardAssetProfile AssetProfile =>
        new(
            PortraitPath:
                $"{Entry.ResPath}/images/cards/XiaoGuangGu.png"
        );

    public XiaoGuangGu()
        : base(
            baseCost: 0,
            type: CardType.Skill,
            rarity: CardRarity.Common,
            target: TargetType.AnyEnemy
        )
    {
        SetDao(Dao.GuangDao);
        this.SecondaryCosts().Set(YuanQiSystem.ResourceId, 1);
        RefreshRankValues();
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

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            target,
            DynamicVars.Weak.IntValue,
            Owner.Creature,
            this
        );
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            target,
            DynamicVars.Vulnerable.IntValue,
            Owner.Creature,
            this
        );

        if (GuRank >= 6 &&
            target.GetPower<ZhaoPoPower>() is
                { Amount: > 0 } zhaoPo)
        {
            // 仙蛊只提供固定增量，避免每次催动把照破指数翻倍。
            await PowerCmd.ModifyAmount(
                choiceContext,
                zhaoPo,
                GuRank switch
                {
                    <= 7 => 1,
                    _ => 2,
                },
                Owner.Creature,
                this
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
        int amount = GuRank switch
        {
            <= 2 => 1,
            <= 4 => 2,
            <= 7 => 3,
            _ => 4,
        };
        DynamicVars.Weak.BaseValue = amount;
        DynamicVars.Vulnerable.BaseValue = amount;
    }
}
