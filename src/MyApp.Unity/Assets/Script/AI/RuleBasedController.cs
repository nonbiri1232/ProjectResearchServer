using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 学習初期の固定対戦相手。ニューラルネットは使わず、合法手の中から
/// 致死攻撃、有利交換、盤面価値の高いプレイを優先する。
/// </summary>
public class RuleBasedController : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f)]
    private float randomActionRate = 0.15f;

    private Player player;
    private Player enemy;
    private GameManager game;
    private int lastTick = -1;

    public void Initialize(Player me, Player opponent, GameManager manager)
    {
        player = me;
        enemy = opponent;
        game = manager;
        lastTick = manager != null ? manager.decisionTick - 1 : -1;
        enabled = true;
    }

    private void Update()
    {
        if (!CanAct() || game.decisionTick == lastTick) return;

        lastTick = game.decisionTick;
        PlayerAction action = SelectAction();
        if (action == null)
        {
            Debug.LogError("RuleBasedController: 合法な行動を作成できませんでした。");
            return;
        }

        int tickBeforeAction = game.decisionTick;
        bool succeeded = game.ExecuteAction(player, enemy, action);
        if (!succeeded && game.currentState == GameState.WaitingForInput &&
            game.decisionTick == tickBeforeAction)
        {
            // ルール判定の食い違いで対局が停止しないようにする。
            game.decisionTick++;
        }
    }

    private bool CanAct()
    {
        if (game == null || player == null || enemy == null ||
            game.currentState != GameState.WaitingForInput)
        {
            return false;
        }

        if (game.currentPhase == PhaseState.Start && game.systemTurn == 1)
        {
            return game.NeedsMarigan(player);
        }

        return game.turn == player;
    }

    private PlayerAction SelectAction()
    {
        if (game.currentPhase == PhaseState.Start)
        {
            return game.systemTurn == 1
                ? SelectMarigan()
                : new PlayerAction(ActionType.SelfGarbage, new List<Card>());
        }

        PlayerAction lethal = FindLethalAttack();
        if (lethal != null) return lethal;

        List<PlayerAction> attacks = BuildAttackActions();
        List<PlayerAction> plays = BuildPlayActions();

        if (Random.value < randomActionRate)
        {
            List<PlayerAction> candidates = new List<PlayerAction>();
            candidates.AddRange(attacks);
            candidates.AddRange(plays);
            candidates.Add(new PlayerAction(ActionType.End));
            return candidates[Random.Range(0, candidates.Count)];
        }

        PlayerAction bestAttack = FindBestAttack(attacks, out float attackScore);
        PlayerAction bestPlay = FindBestPlay(plays, out float playScore);

        if (bestAttack != null && attackScore >= playScore && attackScore > 0f)
            return bestAttack;
        if (bestPlay != null)
            return bestPlay;
        if (bestAttack != null && attackScore > -2f)
            return bestAttack;

        return new PlayerAction(ActionType.End);
    }

    private PlayerAction SelectMarigan()
    {
        List<Card> replace = new List<Card>();
        foreach (Card card in player.hand)
        {
            // 序盤に使いにくい高コスト札を引き直す。
            if (card != null && card.Cost >= 5)
            {
                replace.Add(card);
            }
        }
        return new PlayerAction(ActionType.Marigan, replace);
    }

    private PlayerAction FindLethalAttack()
    {
        foreach (Card attacker in player.field)
        {
            if (LegalActionGenerator.CanDirectAttack(attacker, enemy) &&
                attacker.Attack >= enemy.maxMemory)
            {
                return new PlayerAction(ActionType.Attack, attacker);
            }
        }
        return null;
    }

    private List<PlayerAction> BuildAttackActions()
    {
        List<PlayerAction> result = new List<PlayerAction>();
        List<Card> validTargets = LegalActionGenerator.GetValidAttackTargets(enemy);

        foreach (Card attacker in player.field)
        {
            if (!LegalActionGenerator.CanAttack(game, player, enemy, attacker))
                continue;

            if (LegalActionGenerator.CanDirectAttack(attacker, enemy))
            {
                result.Add(new PlayerAction(ActionType.Attack, attacker));
                continue;
            }

            foreach (Card target in validTargets)
            {
                result.Add(new PlayerAction(
                    ActionType.Attack,
                    attacker,
                    new List<Card>() { target }));
            }
        }

        return result;
    }

    private List<PlayerAction> BuildPlayActions()
    {
        List<PlayerAction> result = new List<PlayerAction>();

        foreach (Card card in player.hand)
        {
            if (!LegalActionGenerator.CanPlay(game, player, enemy, card))
                continue;

            List<Card> validTargets = LegalActionGenerator.GetValidPlayTargets(
                player, enemy, card);
            Card target = SelectPlayTarget(card, validTargets);
            PlayerAction action = target != null
                ? new PlayerAction(
                    ActionType.Play, card, new List<Card>() { target })
                : new PlayerAction(ActionType.Play, card);

            action.isAddCost = LegalActionGenerator.CanPayAdditionalCost(
                player, card);
            result.Add(action);
        }

        return result;
    }

    private static Card SelectPlayTarget(Card source, List<Card> targets)
    {
        if (source == null || targets == null || targets.Count == 0) return null;

        Card best = targets[0];
        float bestValue = BoardEvaluator.EvaluateCard(best);
        foreach (Card target in targets)
        {
            float value = BoardEvaluator.EvaluateCard(target);
            if (source.select.whereTarget == where.hand)
            {
                // 手札を戻す効果では、価値の低い札を優先する。
                if (value < bestValue)
                {
                    best = target;
                    bestValue = value;
                }
            }
            else if (value > bestValue)
            {
                best = target;
                bestValue = value;
            }
        }
        return best;
    }

    private static PlayerAction FindBestAttack(
        List<PlayerAction> actions,
        out float bestScore)
    {
        PlayerAction best = null;
        bestScore = float.NegativeInfinity;

        foreach (PlayerAction action in actions)
        {
            Card attacker = action.sourceCard;
            Card target = action.targetCard != null && action.targetCard.Count == 1
                ? action.targetCard[0]
                : null;

            float score;
            if (target == null)
            {
                score = attacker.Attack * 2f;
            }
            else
            {
                bool killsTarget = attacker.Attack >= target.Hp || attacker.isSegfault;
                bool losesAttacker = target.Attack >= attacker.Hp || target.isSegfault;
                score = killsTarget ? BoardEvaluator.EvaluateCard(target) : -1f;
                if (losesAttacker) score -= BoardEvaluator.EvaluateCard(attacker);
                if (!losesAttacker) score += 1f;
            }

            if (score > bestScore)
            {
                best = action;
                bestScore = score;
            }
        }

        return best;
    }

    private static PlayerAction FindBestPlay(
        List<PlayerAction> actions,
        out float bestScore)
    {
        PlayerAction best = null;
        bestScore = float.NegativeInfinity;

        foreach (PlayerAction action in actions)
        {
            float score = BoardEvaluator.EvaluateCard(action.sourceCard);
            if (action.sourceCard.isImmediate) score += 1f;
            if (action.isAddCost) score += 0.75f;

            if (score > bestScore)
            {
                best = action;
                bestScore = score;
            }
        }

        return best;
    }
}
