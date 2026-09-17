using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public enum TrainingOpponentMode
{
    RuleBased,
    SelfPlay
}

public class TrainingArena : MonoBehaviour
{
    private const string OpponentModeParameter = "training_opponent_mode";

    [Header("対局させる2体のAgent")]
    [SerializeField] private MlAgents agentA;
    [SerializeField] private MlAgents agentB;

    [Header("Training Mode")]
    [Tooltip("RuleBased: 固定教師との学習 / SelfPlay: ML-Agent同士の自己対戦")]
    [SerializeField] private TrainingOpponentMode opponentMode =
        TrainingOpponentMode.RuleBased;

    [Header("AI Deck Pool")]
    [Tooltip("AIに使用させるデッキを登録したカタログ")]
    [SerializeField] private AIDeckCatalog aiDeckCatalog;
    [Tooltip("Agent Aの候補デッキID。空ならカタログ内の全デッキが候補")]
    [SerializeField] private List<int> agentADeckIds = new List<int>();
    [Tooltip("Agent Bの候補デッキID。空ならカタログ内の全デッキが候補")]
    [SerializeField] private List<int> agentBDeckIds = new List<int>();

    [Header("Fallback")]
    [Tooltip("カタログから選べない場合に保存済みデッキを使う")]
    [SerializeField] private bool useSavedDecks = false;

    [Header("Safety Limits")]
    [Tooltip("このsystemTurn数に達した学習対局を引き分けで終了。0以下なら無制限")]
    [SerializeField] private int maxSystemTurns = 200;

    private GameManager gm;
    private Player playerA;
    private Player playerB;
    private RuleBasedController ruleBasedOpponent;
    private bool isStartingNextMatch;
    private int completedMatches;
    private int currentDeckAId = -1;
    private int currentDeckBId = -1;

    private void Start()
    {
        ApplyOpponentModeFromEnvironment();
        ConfigureOpponentController();
        if (!enabled) return;

        if (useSavedDecks)
        {
            DeckManager.LoadDeck();
        }
        StartNewMatch();
    }

    private void ApplyOpponentModeFromEnvironment()
    {
        float configuredMode = Academy.Instance.EnvironmentParameters
            .GetWithDefault(OpponentModeParameter, (float)opponentMode);
        opponentMode = configuredMode < 0.5f
            ? TrainingOpponentMode.RuleBased
            : TrainingOpponentMode.SelfPlay;

        Debug.Log(
            $"【学習】Opponent Mode: {opponentMode} " +
            $"({OpponentModeParameter}={configuredMode:F1})");
    }

    private void Update()
    {
        if (gm == null || isStartingNextMatch || maxSystemTurns <= 0 ||
            gm.currentState == GameState.Finished)
        {
            return;
        }

        if (gm.systemTurn >= maxSystemTurns)
        {
            Debug.LogWarning(
                $"【学習】{maxSystemTurns} system turnsに達したため引き分けで終了します。");
            gm.FinishAsDraw();
        }
    }

    public void StartNewMatch()
    {
        isStartingNextMatch = false;

        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }

        List<Card> deckA = BuildAIDeck(
            agentADeckIds, useSavedDecks ? DeckManager.player1Deck : null,
            out int deckAId);
        List<Card> deckB = BuildAIDeck(
            agentBDeckIds, useSavedDecks ? DeckManager.player2Deck : null,
            out int deckBId);
        currentDeckAId = deckAId;
        currentDeckBId = deckBId;

        playerA = new Player(deckA);
        playerB = new Player(deckB);

        gm = new GameManager(playerA, playerB);

        agentA.enabled = true;
        agentA.Initialize(playerA, playerB, gm);
        if (opponentMode == TrainingOpponentMode.SelfPlay)
        {
            agentB.enabled = true;
            agentB.Initialize(playerB, playerA, gm);
            if (ruleBasedOpponent != null) ruleBasedOpponent.enabled = false;
        }
        else
        {
            agentB.enabled = false;
            ruleBasedOpponent.Initialize(playerB, playerA, gm);
        }

        // Agentが終了報酬を処理した後、次フレームで次の対局を始める。
        gm.OnGameFinished += HandleGameFinished;

        Debug.Log(
            $"【学習】対局開始！ Mode:{opponentMode} " +
            $"Agent A Deck ID:{FormatDeckId(deckAId)} " +
            $"Opponent Deck ID:{FormatDeckId(deckBId)}");
    }

    private void HandleGameFinished(Player winner)
    {
        if (isStartingNextMatch) return;
        isStartingNextMatch = true;
        completedMatches++;

        StatsRecorder stats = Academy.Instance.StatsRecorder;
        stats.Add("CardGame/Match/AgentAWin",
            winner == null ? 0.5f : winner == playerA ? 1f : 0f);
        stats.Add("CardGame/Match/Draw", winner == null ? 1f : 0f);
        stats.Add("CardGame/Match/SystemTurns", gm.systemTurn);
        stats.Add("CardGame/Match/OpponentMode", (float)opponentMode,
            StatAggregationMethod.MostRecent);
        stats.Add("CardGame/Match/DeckAId", currentDeckAId,
            StatAggregationMethod.MostRecent);
        stats.Add("CardGame/Match/DeckBId", currentDeckBId,
            StatAggregationMethod.MostRecent);

        string matchupPrefix =
            $"CardGame/Matchups/{FormatDeckId(currentDeckAId)}_vs_" +
            FormatDeckId(currentDeckBId);
        stats.Add(matchupPrefix + "/AgentAWin", winner == playerA ? 1f : 0f);
        stats.Add(matchupPrefix + "/SystemTurns", gm.systemTurn);

        StartCoroutine(StartNextMatch());
    }

    private IEnumerator StartNextMatch()
    {
        yield return null;
        StartNewMatch();
    }

    private void OnDestroy()
    {
        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }
    }

    private void ConfigureOpponentController()
    {
        if (agentA == null || agentB == null)
        {
            Debug.LogError("TrainingArena: Agent A/Bが設定されていません。");
            enabled = false;
            return;
        }

        ruleBasedOpponent = agentB.GetComponent<RuleBasedController>();
        if (ruleBasedOpponent == null)
        {
            ruleBasedOpponent = agentB.gameObject.AddComponent<RuleBasedController>();
        }
        ruleBasedOpponent.enabled =
            opponentMode == TrainingOpponentMode.RuleBased;
        agentB.enabled = opponentMode == TrainingOpponentMode.SelfPlay;
    }

    private List<Card> BuildAIDeck(
        IReadOnlyList<int> allowedDeckIds,
        List<int> fallbackDeckIds,
        out int selectedDeckId)
    {
        if (aiDeckCatalog != null &&
            aiDeckCatalog.TryCreateRandomDeck(allowedDeckIds, out List<Card> deck,
                out selectedDeckId))
        {
            return deck;
        }

        selectedDeckId = -1;
        return BuildFallbackDeck(fallbackDeckIds);
    }

    private static List<Card> BuildFallbackDeck(List<int> deckIds)
    {
        if (deckIds != null && deckIds.Count > 0)
        {
            List<Card> deck = Player.ChangeCard(deckIds.ToArray());
            if(deck.Count >= 5)return deck;
        }

        return DeckManager.CreateBasicCardDeck();
    }

    private static string FormatDeckId(int deckId)
    {
        return deckId >= 0 ? deckId.ToString() : "Fallback";
    }
}
