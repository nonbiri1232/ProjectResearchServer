using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

// Legacy policy interface: observations=880, discrete branches=[5,8,20,20,2x24].
public class LegacyMlAgents : Agent
{
    public Player myPlayer;
    public Player enemyPlayer;
    public GameManager gm;

    private const int ObservationSize = 880;
    private int lastTick = -1;
    private bool episodeFinished;

    public void Initialize(Player me, Player enemy, GameManager manager)
    {
        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }

        myPlayer = me;
        enemyPlayer = enemy;
        gm = manager;
        episodeFinished = false;
        lastTick = manager != null ? manager.decisionTick - 1 : -1;

        if (gm != null)
        {
            gm.OnGameFinished += HandleGameFinished;
        }
    }

    private void OnDestroy()
    {
        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }
    }

    private void Update()
    {
        if (gm == null || episodeFinished) return;

        if (gm.decisionTick != lastTick)
        {
            lastTick = gm.decisionTick;
            if (ComputeIsMyTurn())
            {
                RequestDecision();
            }
        }
    }

    private bool ComputeIsMyTurn()
    {
        if (gm == null || myPlayer == null || enemyPlayer == null) return false;
        if (gm.currentState != GameState.WaitingForInput) return false;

        if (gm.currentPhase == PhaseState.Start && gm.systemTurn == 1)
        {
            // 初手マリガンは同時進行
            return gm.NeedsMarigan(myPlayer);
        }

        // それ以外(自壊フェーズ・メインフェーズ)は通常のターン制
        return gm.turn == myPlayer;
    }



    private void HandleGameFinished(Player winner)
    {
        if (episodeFinished) return;
        episodeFinished = true;

        Debug.Log($"【学習】対局終了。勝者: {(winner == myPlayer ? "自分" : "相手")}");
        if (winner == myPlayer)
            AddReward(1.0f);
        else if (winner == enemyPlayer)
            AddReward(-1.0f);

        EndEpisode();
    }


    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (episodeFinished || gm == null || myPlayer == null || enemyPlayer == null)
            return;

        if (gm.currentPhase == PhaseState.Start)
        {
            if (gm.systemTurn == 1)
            {
                // マリガン専用フェーズ:Mariganだけ許可
                actionMask.SetActionEnabled(0, (int)ActionType.SelfGarbage, false);
                actionMask.SetActionEnabled(0, (int)ActionType.Play, false);
                actionMask.SetActionEnabled(0, (int)ActionType.Attack, false);
                actionMask.SetActionEnabled(0, (int)ActionType.End, false);
            }
            else
            {
                // 自壊専用フェーズ:SelfGarbageだけ許可
                actionMask.SetActionEnabled(0, (int)ActionType.Marigan, false);
                actionMask.SetActionEnabled(0, (int)ActionType.Play, false);
                actionMask.SetActionEnabled(0, (int)ActionType.Attack, false);
                actionMask.SetActionEnabled(0, (int)ActionType.End, false);
            }
        }
        else // Main
        {
            actionMask.SetActionEnabled(0, (int)ActionType.Marigan, false);
            actionMask.SetActionEnabled(0, (int)ActionType.SelfGarbage, false);

            // 実行可能なカードや攻撃元がない行動は選ばせない。
            if (!myPlayer.hand.Exists(HasBasicPlayRequirements))
                actionMask.SetActionEnabled(0, (int)ActionType.Play, false);

            if (!HasAnyLegalAttack())
                actionMask.SetActionEnabled(0, (int)ActionType.Attack, false);
        }

        bool hasPlayableCard = myPlayer.hand.Exists(HasBasicPlayRequirements);
        for (int i = 0; i < 8; i++)
        {
            bool inHand = i < myPlayer.hand.Count;
            bool validPlayIndex = inHand && HasBasicPlayRequirements(myPlayer.hand[i]);

            // MainでPlay候補がある場合だけ、プレイ不能な手札インデックスを除外する。
            // 候補がない場合はBranch全無効を避けるため0番をダミーとして残す。
            if (!inHand || (gm.currentPhase == PhaseState.Main &&
                hasPlayableCard && !validPlayIndex))
            {
                if (i != 0 || myPlayer.hand.Count > 0)
                    actionMask.SetActionEnabled(1, i, false);
            }
        }

        // Branch 2は自分の場と、where.handの対象選択で共用する。
        int selfTargetLimit = Mathf.Max(Mathf.Max(myPlayer.field.Count, myPlayer.hand.Count), 1);
        for (int i = selfTargetLimit; i < 20; i++)
            actionMask.SetActionEnabled(2, i, false);

        int enemyFieldLimit = Mathf.Max(enemyPlayer.field.Count, 1);
        for (int i = enemyFieldLimit; i < 20; i++)
            actionMask.SetActionEnabled(3, i, false);

        if (gm.currentPhase == PhaseState.Start && gm.systemTurn == 1)
        {
            for (int i = myPlayer.hand.Count; i < 4; i++)
                actionMask.SetActionEnabled(4 + i, 1, false);
        }
        else
        {
            int selectableCount = gm.currentPhase == PhaseState.Start
                ? myPlayer.field.Count
                : Mathf.Max(myPlayer.hand.Count,
                    Mathf.Max(myPlayer.field.Count, enemyPlayer.field.Count));
            for (int i = selectableCount; i < 20; i++)
                actionMask.SetActionEnabled(8 + i, 1, false);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (myPlayer == null || enemyPlayer == null)
        {
            for (int i = 0; i < ObservationSize; i++)
                sensor.AddObservation(0f);
            return;
        }

        //Agentの情報
        sensor.AddObservation(myPlayer.maxMemory);
        sensor.AddObservation(myPlayer.fieldCost);
        sensor.AddObservation(myPlayer.usedMemory);
        sensor.AddObservation(myPlayer.usableMemory);
        sensor.AddObservation(myPlayer.hand.Count);
        sensor.AddObservation(myPlayer.field.Count);
        sensor.AddObservation(myPlayer.deck.Count);
        sensor.AddObservation(myPlayer.garbage.Count);
        //Enemyの情報
        sensor.AddObservation(enemyPlayer.maxMemory);
        sensor.AddObservation(enemyPlayer.fieldCost);
        sensor.AddObservation(enemyPlayer.usedMemory);
        sensor.AddObservation(enemyPlayer.usableMemory);
        sensor.AddObservation(enemyPlayer.hand.Count);
        sensor.AddObservation(enemyPlayer.field.Count);
        sensor.AddObservation(enemyPlayer.deck.Count);
        sensor.AddObservation(enemyPlayer.garbage.Count);

        //手札の情報
        ObserveCard(sensor, myPlayer.hand, 8);
        //Agentのフィールドの情報
        ObserveCard(sensor, myPlayer.field, 20);
        //Enemyのフィールドの情報
        ObserveCard(sensor, enemyPlayer.field, 20);
    }

    private void ObserveCard(VectorSensor sensor, List<Card> cardList,int maxCapacity)
    {
        for(int i = 0; i < maxCapacity; i++)
        {
            if(i < cardList.Count)
            {
                Card card = cardList[i];
                sensor.AddObservation(Card.GetCardId(card));
                sensor.AddObservation(i);
                sensor.AddObservation(card.Attack);
                sensor.AddObservation(card.Cost);
                sensor.AddObservation(card.Hp);

                sensor.AddObservation(card.isDaemon);
                sensor.AddObservation(card.isEncrypted);
                sensor.AddObservation(card.isImmediate);
                sensor.AddObservation(card.isProxy);
                sensor.AddObservation(card.isSandBox);
                sensor.AddObservation(card.isSegfault);
                sensor.AddObservation(card.isCanAttack);
                sensor.AddObservation(card.isFirstTurn);
                sensor.AddObservation(card.isAttacked);
                sensor.AddObservation(card.isAssert);
                sensor.AddObservation(card.Assert);
                sensor.AddObservation(card.attackTimes);
                sensor.AddObservation(CardTypeInt(card.Type));
            }
            else
            {
                sensor.AddObservation(-1);
                sensor.AddObservation(-1);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
                sensor.AddObservation(0);
            }
        }
    }

    private int CardTypeInt(Card.CardType type)
    {
        switch(type)
        {
            case Card.CardType.Object:
                return 0;
            case Card.CardType.Method:
                return 1;
            case Card.CardType.Scope:
                return 2;
            default:
                return -1;
        }
    }

    // Discrete Branch構成 (合計28branch。BehaviorParametersのInspectorで以下のサイズを設定する必要があります)
    //   [5, 8, 20, 20,
    //    2,2,2,2,                                          // 手札マスク x4 (Mariganは初手4枚固定のため)
    //    2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]          // 場マスク x20
    //
    //   Branch 0     (5) : ActionType  0=Marigan, 1=SelfGarbage, 2=Play, 3=Attack, 4=End
    //   Branch 1     (8) : 手札インデックス        (Play の対象選択に使用)
    //   Branch 2     (20): 自分側インデックス       (Attack元 / Play対象がselfField・handの場合)
    //   Branch 3     (20): 相手の場インデックス     (Attack の対象 / Play の対象が enemyField の場合)
    //   Branch 4~7   (2 x4) : マリガン手札マスク。Main時はBranch 4を追加コスト選択に再利用
    //   Branch 8~27  (2 x20): 対象マスク。Start時はSelfGarbage、Main時はPlay対象に使用
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (episodeFinished || !ComputeIsMyTurn()) return;

        var d = actions.DiscreteActions;
        if (d.Length < 28)
        {
            Debug.LogError("MlAgentsには28個のDiscrete Branchが必要です。");
            AddReward(-0.05f);
            return;
        }

        int actionTypeIndex = d[0];
        int handIndex       = d[1];
        int myFieldIndex    = d[2];
        int enemyFieldIndex = d[3];

        ActionType actionType = (ActionType)actionTypeIndex;
        PlayerAction playerAction = null;

        switch (actionType)
        {
            case ActionType.Marigan:
            {
                // 手札マスク(Branch4~7)を見て、引き直したいカードを複数選択する
                // Mariganは初手ドロー直後(手札4枚固定)にしか発生しないため4枠で足りる
                List<Card> target = new List<Card>();
                for (int i = 0; i < myPlayer.hand.Count && i < 4; i++)
                {
                    if (d[4 + i] == 1)
                    {
                        target.Add(myPlayer.hand[i]);
                    }
                }
                // targetが0枚でも「マリガンしない」という有効な選択として扱う
                playerAction = new PlayerAction(ActionType.Marigan, target);
                break;
            }

            case ActionType.SelfGarbage:
            {
                // 場マスク(Branch8~27)を見て、破棄したい自分の場のカードを複数選択する
                List<Card> target = new List<Card>();
                for (int i = 0; i < myPlayer.field.Count && i < 20; i++)
                {
                    if (d[8 + i] == 1)
                    {
                        target.Add(myPlayer.field[i]);
                    }
                }
                playerAction = new PlayerAction(ActionType.SelfGarbage, target);
                break;
            }

            case ActionType.Play:
            {
                if (handIndex >= 0 && handIndex < myPlayer.hand.Count)
                {
                    Card sourceCard = myPlayer.hand[handIndex];
                    if (!HasBasicPlayRequirements(sourceCard)) break;

                    List<Card> targets = BuildPlayTargets(sourceCard, d);
                    playerAction = targets.Count > 0
                        ? new PlayerAction(ActionType.Play, sourceCard, targets)
                        : new PlayerAction(ActionType.Play, sourceCard);

                    // MainではBranch 4を追加コスト選択として再利用する。
                    playerAction.isAddCost = d[4] == 1 && CanPayAdditionalCost(sourceCard);
                }
                break;
            }

            case ActionType.Attack:
            {
                // 自分の場のカードで攻撃する(対象は単一のためBranch2/3をそのまま使用)
                if (myFieldIndex >= 0 && myFieldIndex < myPlayer.field.Count)
                {
                    Card sourceCard = myPlayer.field[myFieldIndex];
                    if (!IsPotentialAttacker(sourceCard)) break;

                    List<Card> validTargets = GetValidAttackTargets();
                    bool enemyHasObjects = enemyPlayer.field.Exists(
                        card => card.Type == Card.CardType.Object);
                    if (!enemyHasObjects)
                    {
                        // GameManagerの仕様上、初ターン中はImmediateでも直接攻撃できない。
                        if (!sourceCard.isFirstTurn)
                            playerAction = new PlayerAction(ActionType.Attack, sourceCard);
                    }
                    else if (validTargets.Count > 0 &&
                             enemyFieldIndex >= 0 && enemyFieldIndex < enemyPlayer.field.Count)
                    {
                        Card selectedTarget = enemyPlayer.field[enemyFieldIndex];
                        if (validTargets.Contains(selectedTarget))
                        {
                            playerAction = new PlayerAction(
                                ActionType.Attack, sourceCard, new List<Card>() { selectedTarget });
                        }
                    }
                }
                break;
            }

            case ActionType.End:
            {
                playerAction = new PlayerAction(ActionType.End);
                break;
            }
        }

        // インデックスが不正で行動を組み立てられなかった場合
        if (playerAction == null)
        {
            AddReward(-0.05f);
            gm.decisionTick++;
            return;
        }

        int tickBeforeAction = gm.decisionTick;
        bool isCorrect = gm.ExecuteAction(myPlayer, enemyPlayer, playerAction);

        // GameManagerの早期return経路でも、次の判断要求が止まらないようにする。
        if (!isCorrect && gm.currentState == GameState.WaitingForInput &&
            gm.decisionTick == tickBeforeAction)
        {
            gm.decisionTick++;
        }

        if (!isCorrect)
        {
            // ルール上実行できない行動を選んだ場合のペナルティ
            AddReward(-0.05f);
        }
        else if (gm.currentState != GameState.Finished)
        {
            // 有効行動の反復で報酬を稼ぐことを防ぎ、短い手数での勝利を促す。
            AddReward(-0.001f);
        }

        // 勝敗による最終報酬(+1 / -1)は GameManager.OnGameFinished イベント側で
        // AddReward() と EndEpisode() を呼ぶ設計を想定しています。
    }

    private bool HasBasicPlayRequirements(Card card)
    {
        if (card == null || !myPlayer.hand.Contains(card)) return false;

        bool ignoreAssert = myPlayer.field.Exists(c => c is ForcedDebugMode);
        if (myPlayer.fieldCost + card.Cost > myPlayer.maxMemory) return false;
        if (myPlayer.usedMemory + card.Cost > myPlayer.usableMemory) return false;
        if (card.isAssert && !ignoreAssert && myPlayer.maxMemory > card.Assert) return false;
        if (card is DeepArchive && myPlayer.garbage.Count < 10) return false;

        // 対象指定は0枚から上限枚数まで任意なので、候補の有無でプレイを禁止しない。
        return true;
    }

    private List<Card> BuildPlayTargets(Card sourceCard, ActionSegment<int> actions)
    {
        List<Card> targets = new List<Card>();
        if (sourceCard.select == null || !sourceCard.select.isSelectConstructor)
            return targets;

        List<Card> pool = GetPlayTargetPool(sourceCard.select.whereTarget);
        int limit = Mathf.Min(sourceCard.select.numOfSelect, pool.Count);
        for (int i = 0; i < pool.Count && i < 20 && targets.Count < limit; i++)
        {
            if (actions[8 + i] != 1) continue;

            Card candidate = pool[i];
            if (sourceCard.ValidateTargets(
                myPlayer, enemyPlayer, new List<Card>() { candidate }))
            {
                targets.Add(candidate);
            }
        }

        // 組み合わせとして無効なら、ルール上有効な「対象0枚」に戻す。
        if (targets.Count > 0 &&
            !sourceCard.ValidateTargets(myPlayer, enemyPlayer, targets))
        {
            targets.Clear();
        }
        return targets;
    }

    private List<Card> GetPlayTargetPool(where targetArea)
    {
        switch (targetArea)
        {
            case where.hand:
                return myPlayer.hand;
            case where.selfField:
                return myPlayer.field;
            case where.enemyField:
                return enemyPlayer.field;
            default:
                return new List<Card>();
        }
    }

    private bool CanPayAdditionalCost(Card card)
    {
        return card != null &&
               card.Type == Card.CardType.Object &&
               myPlayer.fieldCost + card.Cost + 1 <= myPlayer.maxMemory &&
               myPlayer.usedMemory + card.Cost + 1 <= myPlayer.usableMemory;
    }

    private static bool IsPotentialAttacker(Card card)
    {
        return card != null &&
               card.Type == Card.CardType.Object &&
               card.isCanAttack &&
               (!card.isFirstTurn || card.isImmediate) &&
               card.isAttacked < card.attackTimes;
    }

    private bool HasAnyLegalAttack()
    {
        bool enemyHasObjects = enemyPlayer.field.Exists(
            card => card.Type == Card.CardType.Object);
        List<Card> validTargets = GetValidAttackTargets();

        foreach (Card attacker in myPlayer.field)
        {
            if (!IsPotentialAttacker(attacker)) continue;
            if (enemyHasObjects && validTargets.Count > 0) return true;
            if (!enemyHasObjects && !attacker.isFirstTurn) return true;
        }
        return false;
    }

    private List<Card> GetValidAttackTargets()
    {
        List<Card> objects = enemyPlayer.field.FindAll(
            card => card.Type == Card.CardType.Object);
        bool hasProxy = objects.Exists(card => card.isProxy);

        return objects.FindAll(card =>
            !card.isEncrypted && (!hasProxy || card.isProxy));
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        for (int i = 0; i < d.Length; i++) d[i] = 0;

        if (gm == null) return;

        int actionType = gm.currentPhase == PhaseState.Start
            ? (gm.systemTurn == 1 ? 0 : 1)
            : 4;
        Keyboard keyboard = Keyboard.current;

        // 1:Marigan 2:SelfGarbage 3:Play 4:Attack 5:End
        if (keyboard != null)
        {
            if (keyboard.digit1Key.isPressed) actionType = 0;
            else if (keyboard.digit2Key.isPressed) actionType = 1;
            else if (keyboard.digit3Key.isPressed) actionType = 2;
            else if (keyboard.digit4Key.isPressed) actionType = 3;
            else if (keyboard.digit5Key.isPressed) actionType = 4;
        }
        d[0] = actionType;

        int handIndex = 0;
        if (keyboard != null && keyboard.wKey.isPressed) handIndex = 1;
        else if (keyboard != null && keyboard.eKey.isPressed) handIndex = 2;
        else if (keyboard != null && keyboard.rKey.isPressed) handIndex = 3;
        d[1] = handIndex;

        d[2] = 0;
        d[3] = 0;
        Debug.Log($"【Heuristic】{gameObject.name} actionType:{actionType} handIndex:{handIndex}");
    }
}
