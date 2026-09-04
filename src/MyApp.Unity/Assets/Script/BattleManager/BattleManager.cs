using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public abstract class BattleManager : NetworkBehaviour
{
    protected GameManager gm;
    protected Player localPlayer;   // 自分（操作する側）
    protected Player remotePlayer;  // 相手（AI または 通信相手）

    public PhaseState CurrentPhase => gm != null ? gm.currentPhase : PhaseState.Start;
    public bool IsFinished => gm != null && gm.currentState == GameState.Finished;

    public int RequiresTargetCount(CardData cardData)
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

    /// <summary>
    /// 現在、自分が操作可能な状態（自分のターンで、入力待ち）かを確認する
    /// </summary>
    public bool CanAct()
    {
        return gm != null && 
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