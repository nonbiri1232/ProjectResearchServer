using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

// Action Spec: Discrete Branches = [21]
// 10M比較実験はml-agents-configs/*_10m_config.yamlを使用する。
public class MlAgents : Agent
{
    private enum DecisionStage
    {
        SelectAction,
        SelectMarigan,
        SelectSelfGarbage,
        SelectPlaySource,
        SelectPlayTarget,
        SelectAdditionalCost,
        SelectAttackSource,
        SelectAttackTarget
    }

    public Player myPlayer;
    public Player enemyPlayer;
    public GameManager gm;

    [Header("Reward Settings (YAML overrides)")]
    [FormerlySerializedAs("boardRewardScale")]
    [SerializeField, Range(0f, 1f)]
    private float memoryProgressRewardScale = 0.05f;
    [SerializeField] private float terminalWinReward = 1.0f;
    [SerializeField] private float terminalLossReward = -1.0f;
    [SerializeField] private float validActionReward = -0.001f;
    [SerializeField] private float failedActionReward = -0.05f;
    [SerializeField] private float invalidSelectionReward = -0.02f;

    private const int ActionBranchSize = 21;
    private const int ObservationSize = 947;
    private const int MaxHandSize = 8;
    private const int MaxFieldSize = 20;

    private readonly List<Card> pendingTargets = new List<Card>();
    private DecisionStage decisionStage = DecisionStage.SelectAction;
    private ActionType pendingAction;
    private Card pendingSource;
    private bool hasPendingAction;
    private int lastTick = -1;
    private bool episodeFinished;
    private float lastMemoryAdvantage;
    private bool hasMemoryAdvantage;
    private int episodeExecutedActions;
    private int episodeInvalidSelections;

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
        episodeExecutedActions = 0;
        episodeInvalidSelections = 0;
        memoryProgressRewardScale = Academy.Instance.EnvironmentParameters
            .GetWithDefault(
                "memory_progress_reward_scale",
                memoryProgressRewardScale);
        terminalWinReward = GetEnvironmentParameter(
            "terminal_win_reward", terminalWinReward);
        terminalLossReward = GetEnvironmentParameter(
            "terminal_loss_reward", terminalLossReward);
        validActionReward = GetEnvironmentParameter(
            "valid_action_reward", validActionReward);
        failedActionReward = GetEnvironmentParameter(
            "failed_action_reward", failedActionReward);
        invalidSelectionReward = GetEnvironmentParameter(
            "invalid_selection_reward", invalidSelectionReward);
        lastTick = manager != null ? manager.decisionTick - 1 : -1;
        ResetPendingDecision();
        lastMemoryAdvantage = BoardEvaluator.Evaluate(me, enemy, manager);
        hasMemoryAdvantage = true;

        if (gm != null)
        {
            gm.OnGameFinished += HandleGameFinished;
        }

        Academy.Instance.StatsRecorder.Add(
            GetStatsPrefix() + "/MemoryRewardScale",
            memoryProgressRewardScale,
            StatAggregationMethod.MostRecent);
        RecordRewardSettings();
    }

    private static float GetEnvironmentParameter(string name, float fallback)
    {
        return Academy.Instance.EnvironmentParameters.GetWithDefault(name, fallback);
    }

    private void RecordRewardSettings()
    {
        StatsRecorder stats = Academy.Instance.StatsRecorder;
        string prefix = GetStatsPrefix() + "/RewardSettings";
        stats.Add(prefix + "/TerminalWin", terminalWinReward,
            StatAggregationMethod.MostRecent);
        stats.Add(prefix + "/TerminalLoss", terminalLossReward,
            StatAggregationMethod.MostRecent);
        stats.Add(prefix + "/ValidAction", validActionReward,
            StatAggregationMethod.MostRecent);
        stats.Add(prefix + "/FailedAction", failedActionReward,
            StatAggregationMethod.MostRecent);
        stats.Add(prefix + "/InvalidSelection", invalidSelectionReward,
            StatAggregationMethod.MostRecent);
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
                BeginTurnDecision();
            }
        }
    }

    private bool ComputeIsMyTurn()
    {
        if (gm == null || myPlayer == null || enemyPlayer == null) return false;
        if (gm.currentState != GameState.WaitingForInput) return false;

        if (gm.currentPhase == PhaseState.Start && gm.systemTurn == 1)
        {
            return gm.NeedsMarigan(myPlayer);
        }

        return gm.turn == myPlayer;
    }

    private void BeginTurnDecision()
    {
        ApplyMemoryProgressReward();
        ResetPendingDecision();

        if (gm.currentPhase == PhaseState.Start)
        {
            pendingAction = gm.systemTurn == 1
                ? ActionType.Marigan
                : ActionType.SelfGarbage;
            hasPendingAction = true;
            decisionStage = gm.systemTurn == 1
                ? DecisionStage.SelectMarigan
                : DecisionStage.SelectSelfGarbage;
        }
        else
        {
            decisionStage = DecisionStage.SelectAction;
        }

        RecordDecisionStats();
        RequestDecision();
    }

    private void ResetPendingDecision()
    {
        decisionStage = DecisionStage.SelectAction;
        hasPendingAction = false;
        pendingSource = null;
        pendingTargets.Clear();
    }

    private void RequestNextStage(DecisionStage nextStage)
    {
        decisionStage = nextStage;
        RecordDecisionStats();
        RequestDecision();
    }

    private void HandleGameFinished(Player winner)
    {
        if (episodeFinished) return;
        episodeFinished = true;

        Debug.Log($"【学習】対局終了。勝者: {(winner == myPlayer ? "自分" : "相手")}");
        if (winner == myPlayer)
            AddReward(terminalWinReward);
        else if (winner == enemyPlayer)
            AddReward(terminalLossReward);

        StatsRecorder stats = Academy.Instance.StatsRecorder;
        string prefix = GetStatsPrefix();
        stats.Add(prefix + "/Win", winner == myPlayer ? 1f : 0f);
        stats.Add(prefix + "/ExecutedActions", episodeExecutedActions);
        stats.Add(prefix + "/InvalidSelections", episodeInvalidSelections);
        stats.Add(prefix + "/FinalMemoryAdvantage",
            BoardEvaluator.Evaluate(myPlayer, enemyPlayer, gm));

        EndEpisode();
    }

    private void ApplyMemoryProgressReward()
    {
        if (!hasMemoryAdvantage || episodeFinished || gm == null ||
            gm.currentState == GameState.Finished)
        {
            return;
        }

        float currentMemoryAdvantage = BoardEvaluator.Evaluate(
            myPlayer, enemyPlayer, gm);
        float reward = Mathf.Clamp(
            (currentMemoryAdvantage - lastMemoryAdvantage) *
            memoryProgressRewardScale,
            -0.05f,
            0.05f);
        lastMemoryAdvantage = currentMemoryAdvantage;

        if (Mathf.Abs(reward) > 0.000001f)
        {
            AddReward(reward);
            Academy.Instance.StatsRecorder.Add(
                GetStatsPrefix() + "/MemoryProgressReward", reward);
        }
    }

    private void RecordDecisionStats()
    {
        bool[] enabled = BuildEnabledActions();
        int legalChoiceCount = 0;
        foreach (bool isEnabled in enabled)
        {
            if (isEnabled) legalChoiceCount++;
        }

        StatsRecorder stats = Academy.Instance.StatsRecorder;
        string prefix = GetStatsPrefix();
        stats.Add(prefix + "/LegalChoiceCount", legalChoiceCount);
        stats.Add(prefix + "/DecisionStage", (float)decisionStage,
            StatAggregationMethod.Histogram);
    }

    private string GetStatsPrefix()
    {
        return "CardGame/" + gameObject.name;
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (episodeFinished || !ComputeIsMyTurn()) return;

        bool[] enabled = BuildEnabledActions();
        bool hasEnabledAction = false;
        for (int i = 0; i < enabled.Length; i++)
        {
            if (enabled[i])
            {
                hasEnabledAction = true;
            }
            else
            {
                actionMask.SetActionEnabled(0, i, false);
            }
        }

        if (!hasEnabledAction)
        {
            Debug.LogError($"MlAgents: {decisionStage}で有効な行動がありません。");
        }
    }

    private bool[] BuildEnabledActions()
    {
        bool[] enabled = new bool[ActionBranchSize];

        switch (decisionStage)
        {
            case DecisionStage.SelectAction:
                enabled[0] = LegalActionGenerator.HasAnyLegalPlay(
                    gm, myPlayer, enemyPlayer);
                enabled[1] = LegalActionGenerator.HasAnyLegalAttack(
                    gm, myPlayer, enemyPlayer);
                enabled[2] = true;
                break;

            case DecisionStage.SelectMarigan:
                enabled[0] = true;
                for (int i = 0; i < myPlayer.hand.Count && i < 4; i++)
                {
                    enabled[i + 1] = !pendingTargets.Contains(myPlayer.hand[i]);
                }
                break;

            case DecisionStage.SelectSelfGarbage:
                enabled[0] = true;
                for (int i = 0; i < myPlayer.field.Count && i < MaxFieldSize; i++)
                {
                    enabled[i + 1] = !pendingTargets.Contains(myPlayer.field[i]);
                }
                break;

            case DecisionStage.SelectPlaySource:
                for (int i = 0; i < myPlayer.hand.Count && i < MaxHandSize; i++)
                {
                    enabled[i + 1] = LegalActionGenerator.CanPlay(
                        gm, myPlayer, enemyPlayer, myPlayer.hand[i]);
                }
                break;

            case DecisionStage.SelectPlayTarget:
                enabled[0] = true;
                EnableValidPlayTargets(enabled);
                break;

            case DecisionStage.SelectAdditionalCost:
                enabled[0] = true;
                enabled[1] = LegalActionGenerator.CanPayAdditionalCost(
                    myPlayer, pendingSource);
                break;

            case DecisionStage.SelectAttackSource:
                bool hasAttackSource = false;
                for (int i = 0; i < myPlayer.field.Count && i < MaxFieldSize; i++)
                {
                    enabled[i + 1] = LegalActionGenerator.CanAttack(
                        gm, myPlayer, enemyPlayer, myPlayer.field[i]);
                    hasAttackSource |= enabled[i + 1];
                }

                // The board can change between the action-type decision and this
                // follow-up decision. Fall back to the existing cancel action so
                // ML-Agents never receives a fully masked action branch.
                if (!hasAttackSource) enabled[0] = true;
                break;

            case DecisionStage.SelectAttackTarget:
                EnableValidAttackTargets(enabled);
                bool hasTarget = false;
                for (int i = 1; i < enabled.Length; i++) hasTarget |= enabled[i];
                enabled[0] = !hasTarget;
                break;
        }

        return enabled;
    }

    private void EnableValidPlayTargets(bool[] enabled)
    {
        if (pendingSource == null) return;

        List<Card> pool = LegalActionGenerator.GetPlayTargetPool(
            myPlayer, enemyPlayer, pendingSource);
        List<Card> validTargets = LegalActionGenerator.GetValidPlayTargets(
            myPlayer, enemyPlayer, pendingSource);

        for (int i = 0; i < pool.Count && i < MaxFieldSize; i++)
        {
            enabled[i + 1] = validTargets.Contains(pool[i]);
        }
    }

    private void EnableValidAttackTargets(bool[] enabled)
    {
        List<Card> validTargets = LegalActionGenerator.GetValidAttackTargets(enemyPlayer);
        for (int i = 0; i < enemyPlayer.field.Count && i < MaxFieldSize; i++)
        {
            enabled[i + 1] = validTargets.Contains(enemyPlayer.field[i]);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // ML-Agents can invoke CollectObservations while the Agent is being
        // disabled during a scene change, after its VectorSensor was released.
        if (sensor == null) return;

        if (myPlayer == null || enemyPlayer == null || gm == null)
        {
            for (int i = 0; i < ObservationSize; i++)
                sensor.AddObservation(0f);
            return;
        }

        sensor.AddObservation(myPlayer.maxMemory);
        sensor.AddObservation(myPlayer.fieldCost);
        sensor.AddObservation(myPlayer.usedMemory);
        sensor.AddObservation(myPlayer.usableMemory);
        sensor.AddObservation(myPlayer.hand.Count);
        sensor.AddObservation(myPlayer.field.Count);
        sensor.AddObservation(myPlayer.deck.Count);
        sensor.AddObservation(myPlayer.garbage.Count);

        sensor.AddObservation(enemyPlayer.maxMemory);
        sensor.AddObservation(enemyPlayer.fieldCost);
        sensor.AddObservation(enemyPlayer.usedMemory);
        sensor.AddObservation(enemyPlayer.usableMemory);
        sensor.AddObservation(enemyPlayer.hand.Count);
        sensor.AddObservation(enemyPlayer.field.Count);
        sensor.AddObservation(enemyPlayer.deck.Count);
        sensor.AddObservation(enemyPlayer.garbage.Count);

        ObserveCard(sensor, myPlayer.hand, MaxHandSize);
        ObserveCard(sensor, myPlayer.field, MaxFieldSize);
        ObserveCard(sensor, enemyPlayer.field, MaxFieldSize);

        AddOneHot(sensor, (int)gm.currentPhase, 3);
        AddOneHot(sensor, (int)decisionStage, 8);
        AddOneHot(sensor, hasPendingAction ? (int)pendingAction : -1, 5);

        int handSourceIndex = pendingSource != null
            ? myPlayer.hand.IndexOf(pendingSource)
            : -1;
        int fieldSourceIndex = pendingSource != null
            ? myPlayer.field.IndexOf(pendingSource)
            : -1;
        sensor.AddObservation(handSourceIndex >= 0 ? handSourceIndex / 7f : -1f);
        sensor.AddObservation(fieldSourceIndex >= 0 ? fieldSourceIndex / 19f : -1f);

        ObserveSelectedCards(sensor, myPlayer.hand, MaxHandSize);
        ObserveSelectedCards(sensor, myPlayer.field, MaxFieldSize);
        ObserveSelectedCards(sensor, enemyPlayer.field, MaxFieldSize);
        sensor.AddObservation(pendingTargets.Count / 20f);
    }

    private static void AddOneHot(VectorSensor sensor, int value, int size)
    {
        for (int i = 0; i < size; i++)
        {
            sensor.AddObservation(value == i ? 1f : 0f);
        }
    }

    private void ObserveSelectedCards(
        VectorSensor sensor,
        List<Card> cards,
        int maxCapacity)
    {
        for (int i = 0; i < maxCapacity; i++)
        {
            sensor.AddObservation(
                i < cards.Count && pendingTargets.Contains(cards[i]) ? 1f : 0f);
        }
    }

    private static void ObserveCard(
        VectorSensor sensor,
        List<Card> cardList,
        int maxCapacity)
    {
        for (int i = 0; i < maxCapacity; i++)
        {
            if (i < cardList.Count)
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
                for (int value = 2; value < 18; value++)
                    sensor.AddObservation(0);
            }
        }
    }

    private static int CardTypeInt(Card.CardType type)
    {
        switch (type)
        {
            case Card.CardType.Object: return 0;
            case Card.CardType.Method: return 1;
            case Card.CardType.Scope: return 2;
            default: return -1;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (episodeFinished || !ComputeIsMyTurn()) return;

        var discreteActions = actions.DiscreteActions;
        if (discreteActions.Length != 1)
        {
            Debug.LogError("MlAgentsのDiscrete Branchはサイズ21の1個に設定してください。");
            AddReward(invalidSelectionReward);
            return;
        }

        int choice = discreteActions[0];
        bool[] enabled = BuildEnabledActions();
        if (choice < 0 || choice >= enabled.Length || !enabled[choice])
        {
            HandleInvalidSelection(choice);
            return;
        }

        ApplySelectedChoice(choice);
    }

    private void ApplySelectedChoice(int choice)
    {
        switch (decisionStage)
        {
            case DecisionStage.SelectAction:
                ReceiveActionType(choice);
                break;
            case DecisionStage.SelectMarigan:
                ReceiveMarigan(choice);
                break;
            case DecisionStage.SelectSelfGarbage:
                ReceiveSelfGarbage(choice);
                break;
            case DecisionStage.SelectPlaySource:
                ReceivePlaySource(choice);
                break;
            case DecisionStage.SelectPlayTarget:
                ReceivePlayTarget(choice);
                break;
            case DecisionStage.SelectAdditionalCost:
                ExecutePendingAction(choice == 1);
                break;
            case DecisionStage.SelectAttackSource:
                ReceiveAttackSource(choice);
                break;
            case DecisionStage.SelectAttackTarget:
                ReceiveAttackTarget(choice);
                break;
        }
    }

    private void ReceiveActionType(int choice)
    {
        pendingTargets.Clear();
        pendingSource = null;
        hasPendingAction = true;

        switch (choice)
        {
            case 0:
                pendingAction = ActionType.Play;
                RequestNextStage(DecisionStage.SelectPlaySource);
                break;
            case 1:
                pendingAction = ActionType.Attack;
                RequestNextStage(DecisionStage.SelectAttackSource);
                break;
            case 2:
                pendingAction = ActionType.End;
                ExecutePendingAction(false);
                break;
        }
    }

    private void ReceiveMarigan(int choice)
    {
        if (choice == 0)
        {
            ExecutePendingAction(false);
            return;
        }

        Card selected = myPlayer.hand[choice - 1];
        pendingTargets.Add(selected);
        if (pendingTargets.Count >= Mathf.Min(myPlayer.hand.Count, 4))
            ExecutePendingAction(false);
        else
            RequestDecision();
    }

    private void ReceiveSelfGarbage(int choice)
    {
        if (choice == 0)
        {
            ExecutePendingAction(false);
            return;
        }

        pendingTargets.Add(myPlayer.field[choice - 1]);
        if (pendingTargets.Count >= Mathf.Min(myPlayer.field.Count, MaxFieldSize))
            ExecutePendingAction(false);
        else
            RequestDecision();
    }

    private void ReceivePlaySource(int choice)
    {
        if (choice == 0)
        {
            BeginTurnDecision();
            return;
        }

        pendingSource = myPlayer.hand[choice - 1];
        pendingTargets.Clear();

        if (pendingSource.select != null && pendingSource.select.isSelectConstructor)
            RequestNextStage(DecisionStage.SelectPlayTarget);
        else
            RequestNextStage(DecisionStage.SelectAdditionalCost);
    }

    private void ReceivePlayTarget(int choice)
    {
        pendingTargets.Clear();
        if (choice > 0)
        {
            List<Card> pool = LegalActionGenerator.GetPlayTargetPool(
                myPlayer, enemyPlayer, pendingSource);
            pendingTargets.Add(pool[choice - 1]);
        }

        RequestNextStage(DecisionStage.SelectAdditionalCost);
    }

    private void ReceiveAttackSource(int choice)
    {
        if (choice == 0)
        {
            BeginTurnDecision();
            return;
        }

        pendingSource = myPlayer.field[choice - 1];
        pendingTargets.Clear();

        if (LegalActionGenerator.CanDirectAttack(pendingSource, enemyPlayer))
            ExecutePendingAction(false);
        else
            RequestNextStage(DecisionStage.SelectAttackTarget);
    }

    private void ReceiveAttackTarget(int choice)
    {
        if (choice == 0)
        {
            pendingSource = null;
            RequestNextStage(DecisionStage.SelectAttackSource);
            return;
        }

        pendingTargets.Clear();
        pendingTargets.Add(enemyPlayer.field[choice - 1]);
        ExecutePendingAction(false);
    }

    private void ExecutePendingAction(bool addCost)
    {
        PlayerAction playerAction;
        switch (pendingAction)
        {
            case ActionType.Marigan:
            case ActionType.SelfGarbage:
                playerAction = new PlayerAction(
                    pendingAction, new List<Card>(pendingTargets));
                break;
            case ActionType.Play:
            case ActionType.Attack:
                playerAction = pendingTargets.Count > 0
                    ? new PlayerAction(
                        pendingAction, pendingSource, new List<Card>(pendingTargets))
                    : new PlayerAction(pendingAction, pendingSource);
                playerAction.isAddCost = addCost;
                break;
            case ActionType.End:
                playerAction = new PlayerAction(ActionType.End);
                break;
            default:
                HandleInvalidSelection(-1);
                return;
        }

        episodeExecutedActions++;
        Academy.Instance.StatsRecorder.Add(
            GetStatsPrefix() + "/ActionType",
            (float)playerAction.type,
            StatAggregationMethod.Histogram);

        int tickBeforeAction = gm.decisionTick;
        bool isCorrect = gm.ExecuteAction(myPlayer, enemyPlayer, playerAction);
        ResetPendingDecision();

        if (!isCorrect && gm.currentState == GameState.WaitingForInput &&
            gm.decisionTick == tickBeforeAction)
        {
            gm.decisionTick++;
        }

        if (!isCorrect)
        {
            episodeExecutedActions--;
            AddReward(failedActionReward);
        }
        else if (gm.currentState != GameState.Finished)
        {
            ApplyMemoryProgressReward();
            AddReward(validActionReward);
        }
    }

    private void HandleInvalidSelection(int choice)
    {
        Debug.LogWarning(
            $"MlAgents: stage={decisionStage}で無効な選択 {choice} を受信しました。");
        episodeInvalidSelections++;
        AddReward(invalidSelectionReward);

        // A mask can become stale when multiple decision stages are requested in
        // the same frame. Re-requesting the same decision here lets a collapsed
        // policy select the same invalid action forever, so execute a currently
        // legal fallback instead. At the top level, ending the turn is always the
        // safest fallback and guarantees that the match continues.
        bool[] enabled = BuildEnabledActions();
        int fallbackChoice = -1;
        if (decisionStage == DecisionStage.SelectAction && enabled[2])
        {
            fallbackChoice = 2;
        }
        else if (enabled[0])
        {
            fallbackChoice = 0;
        }
        else
        {
            for (int i = 1; i < enabled.Length; i++)
            {
                if (!enabled[i]) continue;
                fallbackChoice = i;
                break;
            }
        }

        if (fallbackChoice >= 0)
        {
            Debug.LogWarning(
                $"MlAgents: 合法なフォールバック {fallbackChoice} を実行します。");
            ApplySelectedChoice(fallbackChoice);
            return;
        }

        // This should not be reachable because every stage provides either an
        // action or a cancel choice. Finish the whole match so TrainingArena can
        // start a fresh one instead of leaving the other agent behind.
        Debug.LogError($"MlAgents: stage={decisionStage}に合法な行動がありません。対局を引き分けで終了します。");
        if (gm != null && gm.currentState != GameState.Finished)
        {
            gm.FinishAsDraw();
        }
        else
        {
            episodeFinished = true;
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        if (discreteActions.Length == 0) return;

        int choice = GetDefaultHeuristicChoice();
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.isPressed) choice = 0;
            else if (keyboard.digit2Key.isPressed) choice = 1;
            else if (keyboard.digit3Key.isPressed) choice = 2;
            else if (keyboard.digit4Key.isPressed) choice = 3;
            else if (keyboard.digit5Key.isPressed) choice = 4;
            else if (keyboard.digit6Key.isPressed) choice = 5;
            else if (keyboard.digit7Key.isPressed) choice = 6;
            else if (keyboard.digit8Key.isPressed) choice = 7;
            else if (keyboard.digit9Key.isPressed) choice = 8;
        }

        discreteActions[0] = choice;
    }

    private int GetDefaultHeuristicChoice()
    {
        bool[] enabled = BuildEnabledActions();

        if (decisionStage == DecisionStage.SelectAction && enabled[2]) return 2;

        if ((decisionStage == DecisionStage.SelectMarigan ||
             decisionStage == DecisionStage.SelectSelfGarbage ||
             decisionStage == DecisionStage.SelectPlayTarget ||
             decisionStage == DecisionStage.SelectAdditionalCost) && enabled[0])
        {
            return 0;
        }

        for (int i = 0; i < enabled.Length; i++)
        {
            if (enabled[i]) return i;
        }

        return 0;
    }
}
