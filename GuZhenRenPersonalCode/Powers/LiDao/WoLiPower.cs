using GuZhenRen.Cards.LiDao;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace GuZhenRen.Powers.LiDao;

/// <summary>
/// 我力蛊的持续战斗状态。
/// 5转只统计自然显化，每 3 次获得 1 点力量；
/// 6转起统计全部实际显化（含群力额外显化），每 2 次获得 1 点力量；
/// 7转起同回合 3 种不同力道虚影实际显化后额外获得 1 点力量（每回合一次）。
/// “不同种类”按具体虚影类型统计，因此我力虚影也可作为其中一种。
/// </summary>
[RegisterPower]
public sealed class WoLiPower : ModPowerTemplate
{
    private const string ManifestCountVar = "ManifestCount";

    private int _lastDistinctTurn;
    private readonly HashSet<Type> _distinctPhantomTypes = [];
    private bool _distinctBonusUsed;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => ManifestCount;

    public int Rank => DynamicVars["Rank"].IntValue;

    public int ManifestCount => Math.Max(0, DynamicVars[ManifestCountVar].IntValue);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Rank", 5m),
        new DynamicVar(ManifestCountVar, 0m),
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://GuZhenRenPersonal/images/power/LiShiPower-64x64.png",
        BigIconPath: "res://GuZhenRenPersonal/images/power/LiShiPower-256x256.png"
    );

    internal void ConfigureRank(int rank)
    {
        DynamicVars["Rank"].BaseValue = Math.Clamp(rank, 5, 7);
        InvokeDisplayAmountChanged();
    }

    internal async Task RecordManifestAsync(
        PlayerChoiceContext choiceContext,
        AbstractLiDaoXuYing phantom,
        bool isGroupExtra
    )
    {
        int rank = Rank;
        if (rank < 6 && isGroupExtra)
        {
            return;
        }

        int count = ManifestCount + 1;
        DynamicVars[ManifestCountVar].BaseValue = count;
        InvokeDisplayAmountChanged();

        if (rank >= 7)
        {
            int turn = CurrentTurn;
            if (turn != _lastDistinctTurn)
            {
                _lastDistinctTurn = turn;
                _distinctPhantomTypes.Clear();
                _distinctBonusUsed = false;
            }

            // 按具体虚影类型统计“不同种类”，而不是只看 BeastKind。
            // 这样我力虚影以及未来新增的非兽力力道虚影，也能触发
            // 原本由不同兽力虚影驱动的联动效果。
            _distinctPhantomTypes.Add(phantom.GetType());
            if (!_distinctBonusUsed &&
                _distinctPhantomTypes.Count >= WoLiGu.DistinctPhantomThreshold)
            {
                _distinctBonusUsed = true;
                await GrantStrengthAsync(choiceContext, 1);
            }
        }

        int threshold = WoLiGu.ManifestsPerStrengthAtRank(rank);
        int remaining = ManifestCount;
        while (remaining >= threshold)
        {
            remaining -= threshold;
            DynamicVars[ManifestCountVar].BaseValue = remaining;
            InvokeDisplayAmountChanged();
            await GrantStrengthAsync(choiceContext, 1);
        }
    }

    private async Task GrantStrengthAsync(
        PlayerChoiceContext choiceContext,
        int amount
    )
    {
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            amount,
            Owner,
            null
        );
    }

    private int CurrentTurn =>
        Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;
}
