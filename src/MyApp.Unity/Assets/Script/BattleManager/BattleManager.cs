using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public abstract class BattleManager : NetworkBehaviour
{
    protected GameManager gm;
    protected Player localPlayer;   // 自分（操作する側）
    protected Player remotePlayer;  // 相手（AI または 通信相手）

    public virtual PhaseState CurrentPhase => gm != null ? gm.currentPhase : PhaseState.Start;
    public virtual bool IsFinished => gm != null && gm.currentState == GameState.Finished;

    protected bool isPresenting;

    public virtual bool CanAttackTarget(CardData attackerData, CardData? targetData = null)
    {
        if (!CanAct() || CurrentPhase != PhaseState.Main || remotePlayer == null) return false;
        Card attacker = localPlayer.field.FirstOrDefault(c => c.uniqueId == attackerData.uniqueId);
        if (attacker == null || attacker.Type != Card.CardType.Object || !attacker.isCanAttack ||
            (attacker.isFirstTurn && !attacker.isImmediate) || attacker.isAttacked >= attacker.attackTimes)
            return false;

        var objects = remotePlayer.field.Where(c => c.Type == Card.CardType.Object).ToList();
        if (!targetData.HasValue) return objects.Count == 0 && !attacker.isFirstTurn;
        Card target = objects.FirstOrDefault(c => c.uniqueId == targetData.Value.uniqueId);
        return target != null && !target.isEncrypted &&
               (!objects.Any(c => c.isProxy) || target.isProxy);
    }

    public virtual int RequiresTargetCount(CardData cardData)
    {
        if (localPlayer == null) return 0;

        // 手札の中から、渡された uniqueId と完全に一致するカードを探し出す
        Card actualCard = localPlayer.hand.FirstOrDefault(c => c.uniqueId == cardData.uniqueId);

        // カードが見つかり、かつ選択が必要な効果（Select）を持っているか確認
        if (actualCard != null && actualCard.select != null && actualCard.select.isSelectConstructor)
        {
            return actualCard.select.numOfSelect;
        }

        // 対象不要なカード（またはエラー）の場合は0を返す
        return 0;
    }

    public virtual bool TryGetPlayTargets(CardData cardData, out where targetArea, out List<int> targetUniqueIds)
    {
        targetArea = where.None;
        targetUniqueIds = new List<int>();
        if (localPlayer == null || remotePlayer == null) return false;

        Card source = localPlayer.hand.FirstOrDefault(c => c.uniqueId == cardData.uniqueId);
        if (source == null || source.select == null || !source.select.isSelectConstructor)
            return false;

        targetArea = source.select.whereTarget;
        List<Card> candidates = source.select.numOfSelect == 1
            ? LegalActionGenerator.GetValidPlayTargets(localPlayer, remotePlayer, source)
            : LegalActionGenerator.GetPlayTargetPool(localPlayer, remotePlayer, source);
        targetUniqueIds = candidates
            .Where(c => c != null && c.uniqueId != source.uniqueId)
            .Select(c => c.uniqueId)
            .Distinct()
            .ToList();
        return targetUniqueIds.Count >= source.select.numOfSelect;
    }

    /// <summary>
    /// 現在、自分が操作可能な状態（自分のターンで、入力待ち）かを確認する
    /// </summary>
    public virtual bool CanAct()
    {
        return !isPresenting && localPlayer != null && gm != null &&
               gm.currentState == GameState.WaitingForInput && 
               gm.turn == localPlayer;
    }

    public abstract void SubmitPlay(CardData sourceData, bool addCost, List<CardData> targetDatas = null);
    
    public abstract void SubmitAttack(CardData attackerData, CardData? targetData = null);
    
    public abstract void SubmitEndTurn();
    
    public abstract void SubmitMarigan(List<CardData> selectedCardsData);
    
    public abstract void SubmitSelfGarbage(List<CardData> selectedCardsData);

    protected Player GetEnemyPlayer(Player p)
    {
        return p == localPlayer ? remotePlayer : localPlayer;
    }
}
