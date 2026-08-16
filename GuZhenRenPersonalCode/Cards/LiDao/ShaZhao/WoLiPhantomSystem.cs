using GuZhenRen.Cards.ShaZhao;
using GuZhenRen.Characters;
using GuZhenRen.Powers.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Utils;

namespace GuZhenRen.Cards.LiDao;

/// <summary>
/// 万我生成的“我力虚影”与独立临时生命池。
///
/// 我力虚影：每个提供 5 点独立来源临时生命；第一次显化必定成功，
/// 之后按万我转数决定显化率（6/7/8/9转 = 25/30/35/40%）。
/// 显化成功时复制本次触发动作 50% 的可复制效果（伤害按段、格挡）。
/// </summary>
public static class WoLiPhantomSystem
{
    internal static async Task AddShadowsAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        int rank,
        int count
    )
    {
        if (count <= 0)
        {
            return;
        }

        for (int index = 0; index < count; index++)
        {
            WoLiXuYing phantom = GuGeneratedCardFactory.Create<WoLiXuYing>(
                owner,
                rank,
                upgraded: false
            );
            await GuGeneratedCardFactory.AddToHandOrDiscard(
                phantom,
                owner
            );
        }

        WoLiTempHpPower? existing =
            owner.Creature.GetPower<WoLiTempHpPower>();
        if (existing == null)
        {
            WoLiTempHpPower power =
                (WoLiTempHpPower)ModelDb.Power<WoLiTempHpPower>().ToMutable();
            power.AddShadows(count);
            await PowerCmd.Apply(
                choiceContext,
                power,
                owner.Creature,
                count,
                owner.Creature,
                null
            );
        }
        else
        {
            existing.AddShadows(count);
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                count,
                owner.Creature,
                null
            );
        }
    }

    internal static async Task ConsumeShadowsAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        int count
    )
    {
        if (count <= 0)
        {
            return;
        }

        WoLiXuYing[] shadows = LiDaoPhantomSystem
            .GetPermanentPhantoms(owner)
            .OfType<WoLiXuYing>()
            .Take(count)
            .ToArray();

        foreach (WoLiXuYing shadow in shadows)
        {
            await CardPileCmd.RemoveFromCombat(
                shadow,
                skipVisuals: false
            );
        }
    }

    internal static async Task ExecuteCopyAsync(
        WoLiXuYing shadow,
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    )
    {
        CardModel source = triggeringPlay.Card;
        const decimal copyRatio = 0.5m;

        if (source.Type == CardType.Attack &&
            target != null &&
            TryGetDynamicValue(source, "Damage", out decimal damage))
        {
            int hits = TryGetDynamicInt(source, "Hits", 1);
            int perHit = Math.Max(
                0,
                (int)Math.Floor(damage * copyRatio)
            );
            for (int index = 0; index < hits; index++)
            {
                if (perHit <= 0 || !target.IsAlive)
                {
                    break;
                }

                await DamageCmd.Attack(perHit)
                    .FromCard(shadow, cardPlay: null)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }
        }

        if (TryGetDynamicValue(source, "Block", out decimal block))
        {
            int copiedBlock = Math.Max(
                0,
                (int)Math.Floor(block * copyRatio)
            );
            if (copiedBlock > 0)
            {
                await CreatureCmd.GainBlock(
                    shadow.Owner.Creature,
                    new BlockVar(copiedBlock, ValueProp.Move),
                    cardPlay: null
                );
            }
        }
    }

    private static int TryGetDynamicInt(
        CardModel card,
        string key,
        int fallback
    )
    {
        try
        {
            return Math.Max(1, card.DynamicVars[key].IntValue);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool TryGetDynamicValue(
        CardModel card,
        string key,
        out decimal value
    )
    {
        try
        {
            value = card.DynamicVars[key].BaseValue;
            return value > 0;
        }
        catch
        {
            value = 0m;
            return false;
        }
    }
}

[RegisterCard(typeof(GuZhenRenXuYingCardPool))]
public sealed class WoLiXuYing : AbstractLiDaoXuYing
{
    private static readonly SavedAttachedState<CardModel, bool>
        HasManifestedState = new(
            Entry.ModId + ".li_dao.wo_li_phantom.manifested",
            static () => false
        );

    public override LiDaoBeastKind? BeastKind => null;

    public override int PhantomSlotCost => 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("RepeatChance", 25m)];

    public WoLiXuYing() : base(CardType.Attack, TargetType.AnyEnemy)
    {
        SetDao(Dao.LiDao);
        RefreshRankValues();
    }

    protected override void AddExtraArgsToDescription(
        MegaCrit.Sts2.Core.Localization.LocString description
    )
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "RepeatChance",
            WanWo.RepeatManifestChanceAtRank(GuRank)
        );
    }

    protected override Task TriggerPhantomEffect(
        PlayerChoiceContext choiceContext,
        CardPlay triggeringPlay,
        Creature? target
    )
    {
        HasManifestedState[this] = true;
        SetBaseChance(
            WanWo.RepeatManifestChanceAtRank(GuRank) / 100f
        );
        return WoLiPhantomSystem.ExecuteCopyAsync(
            this,
            choiceContext,
            triggeringPlay,
            target
        );
    }

    protected override void OnXuYingGuRankChanged() => RefreshRankValues();

    protected override void OnXuYingGuRankLoaded() => RefreshRankValues();

    private void RefreshRankValues()
    {
        SetBaseChance(
            HasManifestedState[this]
                ? WanWo.RepeatManifestChanceAtRank(GuRank) / 100f
                : 1f
        );
        DynamicVars["RepeatChance"].BaseValue =
            WanWo.RepeatManifestChanceAtRank(GuRank);
    }
}
