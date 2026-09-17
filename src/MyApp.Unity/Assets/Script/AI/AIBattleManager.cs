using System;

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents.Policies;

/// <summary>
/// 人間対学習済みML-Agentsのオフライン対戦を管理する。
/// 人間は保存済みDeck1、AIは指定された候補デッキからランダムに使用する。
/// </summary>
public class AIBattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MlAgents aiAgent;
    [SerializeField] private AIBattleVisual visual;

    [Header("Deck")]
    [SerializeField] private bool useSavedDecks = true;
    [SerializeField] private bool useBasicDeckWhenInvalid = true;

    [Header("AI Deck Pool")]
    [Tooltip("AIに使用させるデッキを登録したカタログ")]
    [SerializeField] private AIDeckCatalog aiDeckCatalog;
    [Tooltip("AIへ渡す候補デッキID。空ならカタログ内の全デッキが候補")]
    [SerializeField] private List<int> aiDeckIds = new List<int>();

    [Header("Turn")]
    [SerializeField] private bool randomizeFirstPlayer;

    [Header("ML-Agents")]
    [Tooltip("ON: Python Trainerへ接続して人間との対戦を学習。OFF: ONNXで推論のみ。")]
    [SerializeField] private bool learnFromHuman;

    private GameManager gm;
    private Player humanPlayer;
    private Player aiPlayer;
    private int observedDecisionTick = -1;

    public event Action BoardChanged;

    public Player HumanPlayer => humanPlayer;
    public Player AIPlayer => aiPlayer;
    public GameManager Game => gm;
    public bool IsHumanTurn => gm != null && gm.turn == humanPlayer;
    public PhaseState CurrentPhase => gm != null ? gm.currentPhase : PhaseState.Start;
    public bool IsFinished => gm != null && gm.currentState == GameState.Finished;
    public bool LearnFromHuman => learnFromHuman;
    public int CompletedMatches { get; private set; }

    private void Start()
    {
        if (aiAgent == null || visual == null)
        {
            Debug.LogError("AIBattleManager: aiAgentまたはvisualが設定されていません。");
            enabled = false;
            return;
        }

        if (useSavedDecks)
        {
            DeckManager.LoadDeck();
        }

        StartBattle();
    }

    private void Update()
    {
        if (gm == null || gm.decisionTick == observedDecisionTick) return;

        observedDecisionTick = gm.decisionTick;
        NotifyBoardChanged();
    }

    private void OnDestroy()
    {
        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }
    }

    private void StartBattle()
    {
        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }

        List<Card> humanDeck = BuildDeck(
            useSavedDecks ? DeckManager.player1Deck : null);
        List<Card> aiDeck = BuildAIDeck(out int selectedAIDeckId);

        Debug.Log(selectedAIDeckId >= 0
            ? $"【AIデッキ選択】Deck ID: {selectedAIDeckId}"
            : "【AIデッキ選択】フォールバックデッキを使用します。");

        humanPlayer = new Player(humanDeck);
        aiPlayer = new Player(aiDeck);

        bool aiGoesFirst = randomizeFirstPlayer && UnityEngine.Random.value < 0.5f;
        Player first = aiGoesFirst ? aiPlayer : humanPlayer;
        Player second = aiGoesFirst ? humanPlayer : aiPlayer;

        gm = new GameManager(first, second);
        gm.OnGameFinished += HandleGameFinished;
        observedDecisionTick = gm.decisionTick;

        if (!ConfigureAgentBehavior()) return;
        aiAgent.Initialize(aiPlayer, humanPlayer, gm);
        visual.Initialize(this);
        NotifyBoardChanged();
    }

    private List<Card> BuildAIDeck(out int selectedDeckId)
    {
        if (aiDeckCatalog != null &&
            aiDeckCatalog.TryCreateRandomDeck(aiDeckIds, out List<Card> deck,
                out selectedDeckId))
        {
            return deck;
        }

        selectedDeckId = -1;
        return BuildDeck(useSavedDecks ? DeckManager.player2Deck : null);
    }

    private List<Card> BuildDeck(List<int> ids)
    {
        if (ids != null && ids.Count > 0)
        {
            List<Card> savedDeck = Player.ChangeCard(ids.ToArray());
            if (savedDeck.Count >= 5) return savedDeck;
        }

        if (useBasicDeckWhenInvalid)
        {
            // The old basic fallback consists of plain Card instances and has no
            // card abilities.  Prefer a real catalog deck so an empty PlayerPrefs
            // save on a new PC does not silently create an effect-less match.
            if (aiDeckCatalog != null &&
                aiDeckCatalog.TryCreateRandomDeck(
                    new List<int> { 0 }, out List<Card> catalogFallback,
                    out int fallbackDeckId))
            {
                Debug.LogWarning(
                    $"保存デッキが見つからないため、実カードのDeck ID: {fallbackDeckId}を使用します。" +
                    "自作デッキを使うにはデッキ作成画面でDeck 1を保存してください。");
                return catalogFallback;
            }

            return DeckManager.CreateBasicCardDeck();
        }

        throw new InvalidOperationException(
            "対戦用デッキが不正です。AI Deck Catalogまたは保存デッキを設定してください。");
    }

    private bool ConfigureAgentBehavior()
    {
        BehaviorParameters behavior = aiAgent.GetComponent<BehaviorParameters>();
        if (behavior == null)
        {
            Debug.LogError("AI AgentにBehaviorParametersがありません。");
            enabled = false;
            return false;
        }

        if (!learnFromHuman && behavior.Model == null)
        {
            Debug.LogError(
                "推論モードにはBehaviorParametersのModel設定が必要です。" +
                "学習する場合はLearn From HumanをONにしてください。");
            enabled = false;
            return false;
        }

        // DefaultはPython Trainer接続時に学習し、未接続時はModelを使用する。
        // InferenceOnlyはInspectorに設定したONNXだけで動作する。
        behavior.BehaviorType = learnFromHuman
            ? BehaviorType.Default
            : BehaviorType.InferenceOnly;

        Debug.Log(learnFromHuman
            ? "【対人学習】Trainer接続待機モードで開始します。"
            : "【AI対戦】学習済みモデルの推論モードで開始します。");
        return true;
    }

    public bool SubmitMarigan(List<Card> cards)
    {
        // 初手マリガンだけは先攻・後攻に関係なく同時進行する。
        if (gm == null || gm.currentState != GameState.WaitingForInput ||
            gm.currentPhase != PhaseState.Start || gm.systemTurn != 1 ||
            !gm.NeedsMarigan(humanPlayer))
        {
            return false;
        }

        return ExecuteHumanAction(new PlayerAction(
            ActionType.Marigan, cards ?? new List<Card>()));
    }

    public bool SubmitSelfGarbage(List<Card> cards)
    {
        if (!CanHumanAct() || gm.currentPhase != PhaseState.Start ||
            gm.systemTurn == 1)
        {
            return false;
        }

        return ExecuteHumanAction(new PlayerAction(
            ActionType.SelfGarbage, cards ?? new List<Card>()));
    }

    public bool CanBeginPlay(Card source, out string reason)
    {
        return ValidatePlayBasics(source, false, out reason);
    }

    public bool PlayCard(Card source, List<Card> targets = null, bool addCost = false)
    {
        if (!ValidatePlay(source, targets, addCost, out string reason))
        {
            Debug.LogWarning($"カードをプレイできません: {reason}");
            return false;
        }

        PlayerAction action = targets != null
            ? new PlayerAction(ActionType.Play, source, targets)
            : new PlayerAction(ActionType.Play, source);
        action.isAddCost = addCost;

        bool result = ExecuteHumanAction(action);
        if (!result)
        {
            Debug.LogWarning(
                $"GameManagerがプレイを拒否しました。Card:{source.GetType().Name}, " +
                $"Cost:{source.Cost}, AddCost:{addCost}, " +
                $"Field:{humanPlayer.fieldCost}/{humanPlayer.maxMemory}, " +
                $"Used:{humanPlayer.usedMemory}/{humanPlayer.usableMemory}");
        }
        return result;
    }

    private bool ValidatePlay(
        Card source, List<Card> targets, bool addCost, out string reason)
    {
        if (!ValidatePlayBasics(source, addCost, out reason)) return false;

        bool needsTargets = source.select != null && source.select.isSelectConstructor;
        if (!needsTargets)
        {
            if (targets != null && targets.Count > 0)
            {
                reason = "このカードは対象を選択しません。";
                return false;
            }
            reason = null;
            return true;
        }

        int selectedCount = targets?.Count ?? 0;
        if (selectedCount == 0)
        {
            reason = null;
            return true;
        }
        if (selectedCount > source.select.numOfSelect)
        {
            reason = $"対象数が上限を超えています（上限:{source.select.numOfSelect}、選択:{selectedCount}）。";
            return false;
        }
        if (targets.Any(card => card == null) ||
            targets.Distinct().Count() != selectedCount)
        {
            reason = "対象にnullまたは重複があります。";
            return false;
        }

        List<Card> pool = GetTargetPool(source.select.whereTarget);
        if (targets.Any(card => !pool.Contains(card)))
        {
            reason = "対象の選択領域が正しくありません。";
            return false;
        }
        if (!source.ValidateTargets(humanPlayer, aiPlayer, targets))
        {
            reason = "カード固有の対象条件を満たしていません。";
            return false;
        }

        reason = null;
        return true;
    }

    private bool ValidatePlayBasics(Card source, bool addCost, out string reason)
    {
        if (!CanHumanAct())
        {
            reason = "現在はあなたが行動できる状態ではありません。";
            return false;
        }
        if (gm.currentPhase != PhaseState.Main)
        {
            reason = "メインフェーズではありません。";
            return false;
        }
        if (source == null || !humanPlayer.hand.Contains(source) ||
            source.player != humanPlayer)
        {
            reason = "カードがあなたの手札にありません。";
            return false;
        }

        int totalCost = source.Cost + (addCost ? 1 : 0);
        if (humanPlayer.fieldCost + totalCost > humanPlayer.maxMemory)
        {
            reason = $"フィールドメモリが不足しています（必要:{totalCost}）。";
            return false;
        }
        if (humanPlayer.usedMemory + totalCost > humanPlayer.usableMemory)
        {
            int remaining = humanPlayer.usableMemory - humanPlayer.usedMemory;
            reason = $"使用可能メモリが不足しています（必要:{totalCost}、残り:{remaining}）。";
            return false;
        }

        bool ignoreAssert = humanPlayer.field.Any(card => card is ForcedDebugMode);
        if (source.isAssert && !ignoreAssert && humanPlayer.maxMemory > source.Assert)
        {
            reason = $"Assert条件を満たしていません（最大メモリを{source.Assert}以下にしてください）。";
            return false;
        }
        // AddCost()はGameManager本処理で呼ばれるため、事前判定では
        // 現在存在する副作用なしの固有条件だけを確認する。
        if (source is DeepArchive && humanPlayer.garbage.Count < 10)
        {
            reason = "DeepArchiveにはガベージが10枚以上必要です。";
            return false;
        }

        reason = null;
        return true;
    }

    private List<Card> GetTargetPool(where targetArea)
    {
        switch (targetArea)
        {
            case where.hand:
                return humanPlayer.hand;
            case where.selfField:
                return humanPlayer.field;
            case where.enemyField:
                return aiPlayer.field;
            default:
                return new List<Card>();
        }
    }

    public bool Attack(Card attacker, Card target = null)
    {
        if (!CanHumanAct() || gm.currentPhase != PhaseState.Main ||
            attacker == null || !humanPlayer.field.Contains(attacker))
        {
            return false;
        }

        PlayerAction action = target == null
            ? new PlayerAction(ActionType.Attack, attacker)
            : new PlayerAction(
                ActionType.Attack, attacker, new List<Card>() { target });
        return ExecuteHumanAction(action);
    }

    public bool EndTurn()
    {
        if (!CanHumanAct() || gm.currentPhase != PhaseState.Main) return false;
        return ExecuteHumanAction(new PlayerAction(ActionType.End));
    }

    private bool CanHumanAct()
    {
        return gm != null && gm.currentState == GameState.WaitingForInput &&
               gm.turn == humanPlayer;
    }

    private bool ExecuteHumanAction(PlayerAction action)
    {
        bool result = gm.ExecuteAction(humanPlayer, aiPlayer, action);
        observedDecisionTick = gm.decisionTick;
        NotifyBoardChanged();
        return result;
    }

    public void StartNextBattle()
    {
        if (gm != null && gm.currentState != GameState.Finished)
        {
            Debug.LogWarning("対戦中は次の対戦を開始できません。");
            return;
        }

        StartBattle();
    }

    public void SurrenderHuman()
    {
        if (gm == null || gm.currentState == GameState.Finished) return;
        gm.Surrender(humanPlayer);
    }

    private void HandleGameFinished(Player winner)
    {
        CompletedMatches++;
        NotifyBoardChanged();
        visual.ShowGameResult(winner == humanPlayer);
        string mode = learnFromHuman ? "対人学習" : "AI対戦";
        Debug.Log(
            $"【{mode}】Episode {CompletedMatches} 終了 / " +
            $"AI結果:{(winner == aiPlayer ? "勝利" : "敗北")}");
    }

    private void NotifyBoardChanged()
    {
        BoardChanged?.Invoke();
        if (visual != null) visual.Refresh();
    }
}
