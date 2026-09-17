using System.Collections.Generic;

/// <summary>
/// AI、教師Bot、UIから共通利用できる副作用のない合法手判定。
/// 最終的なルール検証はGameManager.ExecuteActionが行う。
/// </summary>
public static class LegalActionGenerator
{
    public static bool CanPlay(
        GameManager game,
        Player player,
        Player enemy,
        Card card)
    {
        if (!CanActInMain(game, player, enemy) ||
            card == null ||
            card.player != player ||
            !player.hand.Contains(card))
        {
            return false;
        }

        bool ignoreAssert = player.field.Exists(c => c is ForcedDebugMode);
        if (player.fieldCost + card.Cost > player.maxMemory) return false;
        if (player.usedMemory + card.Cost > player.usableMemory) return false;
        if (card.isAssert && !ignoreAssert && player.maxMemory > card.Assert)
            return false;

        // 現在、AddCost()に固有の事前条件を持つカード。
        // AddCost()自体は将来副作用を持つ可能性があるため、ここからは呼ばない。
        if (card is DeepArchive && player.garbage.Count < 10) return false;

        return true;
    }

    public static bool CanPayAdditionalCost(Player player, Card card)
    {
        return player != null &&
               card != null &&
               card.Type == Card.CardType.Object &&
               player.fieldCost + card.Cost + 1 <= player.maxMemory &&
               player.usedMemory + card.Cost + 1 <= player.usableMemory;
    }

    public static List<Card> GetPlayTargetPool(
        Player player,
        Player enemy,
        Card source)
    {
        if (source == null || source.select == null ||
            !source.select.isSelectConstructor)
        {
            return new List<Card>();
        }

        switch (source.select.whereTarget)
        {
            case where.hand:
                return player != null ? player.hand : new List<Card>();
            case where.selfField:
                return player != null ? player.field : new List<Card>();
            case where.enemyField:
                return enemy != null ? enemy.field : new List<Card>();
            default:
                return new List<Card>();
        }
    }

    public static List<Card> GetValidPlayTargets(
        Player player,
        Player enemy,
        Card source)
    {
        List<Card> result = new List<Card>();
        foreach (Card candidate in GetPlayTargetPool(player, enemy, source))
        {
            if (candidate == null) continue;
            List<Card> target = new List<Card>() { candidate };
            if (source.ValidateTargets(player, enemy, target))
            {
                result.Add(candidate);
            }
        }
        return result;
    }

    public static bool IsPotentialAttacker(Card card)
    {
        return card != null &&
               card.Type == Card.CardType.Object &&
               card.isCanAttack &&
               (!card.isFirstTurn || card.isImmediate) &&
               card.isAttacked < card.attackTimes;
    }

    public static List<Card> GetValidAttackTargets(Player enemy)
    {
        if (enemy == null) return new List<Card>();

        List<Card> objects = enemy.field.FindAll(
            card => card != null && card.Type == Card.CardType.Object);
        bool hasProxy = objects.Exists(card => card.isProxy);

        return objects.FindAll(card =>
            !card.isEncrypted && (!hasProxy || card.isProxy));
    }

    public static bool CanDirectAttack(Card attacker, Player enemy)
    {
        if (!IsPotentialAttacker(attacker) || enemy == null) return false;

        bool enemyHasObjects = enemy.field.Exists(
            card => card != null && card.Type == Card.CardType.Object);

        // GameManagerの仕様ではImmediateでも配置されたターンの直接攻撃は不可。
        return !enemyHasObjects && !attacker.isFirstTurn;
    }

    public static bool CanAttack(
        GameManager game,
        Player player,
        Player enemy,
        Card attacker)
    {
        if (!CanActInMain(game, player, enemy) ||
            attacker == null ||
            !player.field.Contains(attacker) ||
            !IsPotentialAttacker(attacker))
        {
            return false;
        }

        if (CanDirectAttack(attacker, enemy)) return true;

        bool enemyHasObjects = enemy.field.Exists(
            card => card != null && card.Type == Card.CardType.Object);
        return enemyHasObjects && GetValidAttackTargets(enemy).Count > 0;
    }

    public static bool HasAnyLegalPlay(
        GameManager game,
        Player player,
        Player enemy)
    {
        return player != null &&
               player.hand.Exists(card => CanPlay(game, player, enemy, card));
    }

    public static bool HasAnyLegalAttack(
        GameManager game,
        Player player,
        Player enemy)
    {
        return player != null &&
               player.field.Exists(card => CanAttack(game, player, enemy, card));
    }

    private static bool CanActInMain(
        GameManager game,
        Player player,
        Player enemy)
    {
        return game != null &&
               player != null &&
               enemy != null &&
               game.currentState == GameState.WaitingForInput &&
               game.currentPhase == PhaseState.Main &&
               game.turn == player &&
               game.notrun == enemy;
    }
}
