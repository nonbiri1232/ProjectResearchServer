using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using System.Text;
using System.IO;

/// <summary>
/// 新AI・旧AIを自由に組み合わせて自動対戦を管理し、勝率を集計するマネージャー。
/// 描画（UI）処理を完全に排除し、高速なシミュレーションに特化しています。
/// </summary>
public class AIAIBattleManager : MonoBehaviour
{
    [Header("AI 1 - どちらか片方をセット")]
    [SerializeField] private MlAgents ai1_New; 
    [SerializeField] private LegacyMlAgents ai1_Legacy; 

    [Header("AI 2 - どちらか片方をセット")]
    [SerializeField] private MlAgents ai2_New; 
    [SerializeField] private LegacyMlAgents ai2_Legacy; 

    [Header("Match Settings")]
    [Tooltip("自動で対戦を行う最大回数")]
    public int maxMatches = 100000;
    [SerializeField] private bool randomizeFirstPlayer = true;
    [SerializeField, Min(1)] private int progressInterval = 100;
    [SerializeField, Min(1)] private int maxSystemTurns = 200;
    [SerializeField, Min(10)] private float stallTimeoutSeconds = 60f;
    private float lastProgressTime;
    private float nextStatusTime;
    private int observedTick;
    private int observedTurn;
    private GameState observedState;
    private bool runStarted;
    private bool runFinished;
    private string stopReason = "実行中";
    private string reportPath;
    public int Draws => CompletedMatches - AI1Wins - AI2Wins;

    [Header("AI Deck Pool")]
    [SerializeField] private AIDeckCatalog aiDeckCatalog;
    [SerializeField] private List<int> ai1DeckIds = new List<int>();
    [SerializeField] private List<int> ai2DeckIds = new List<int>();

    private GameManager gm;
    private Player aiPlayer1;
    private Player aiPlayer2;

    // 集計用データ
    public int CompletedMatches { get; private set; }
    public int AI1Wins { get; private set; }
    public int AI2Wins { get; private set; }

    // エラー防止用のフラグ
    private bool isStartingNextMatch;

    //この試合で使用しているデッキ
    int currentp1DeckId;
    int currentp2DeckId;
    public class MatchupStat
    {
        public int MatchesCount;
        public int P1Wins;
        public int P2Wins;
    }

    private Dictionary<(int, int), MatchupStat> matchupStats = new Dictionary<(int, int), MatchupStat>();

    private void Start()
    {
        // どちらのAIもセットされていない場合はエラー
        if ((ai1_New == null && ai1_Legacy == null) || (ai2_New == null && ai2_Legacy == null))
        {
            Debug.LogError("AIAIBattleManager: 1P側または2P側のAIが設定されていません。");
            enabled = false;
            return;
        }

        if (maxMatches <= 0 || !ValidateModel(ai1_New != null ? (Agent)ai1_New : ai1_Legacy) ||
            !ValidateModel(ai2_New != null ? (Agent)ai2_New : ai2_Legacy))
        {
            Debug.LogError("AI対戦: 対戦数またはModel設定が不正です。開始しません。");
            enabled = false;
            return;
        }
        reportPath = Path.Combine(Application.dataPath, "Script/AIRanking/BattleData",
            $"AIBattleReport_{System.DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
        runStarted = true;
        nextStatusTime = Time.realtimeSinceStartup + 10f;
        // 超高速化: Unityのゲーム進行スピードをn倍にする
        Time.timeScale = 20f; 

        Debug.Log(
            $"【AI自動対戦】 {GetAgentName(ai1_New, ai1_Legacy)} vs " +
            $"{GetAgentName(ai2_New, ai2_Legacy)} / {maxMatches}戦を開始します。");
        StartNewMatch();
    }

    private void OnDestroy()
    {
        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }
        
        // 途中で止めた時用に時間を元に戻す
        Time.timeScale = 1f; 
    }

    private static bool ValidateModel(Agent agent)
    {
        var behavior = agent != null ? agent.GetComponent<BehaviorParameters>() : null;
        return behavior != null && behavior.Model != null;
    }

    private void OnDisable()
    {
        if (!runStarted || runFinished) return;
        stopReason = "途中停止（未完了の試合は集計対象外）";
        PrintDetailedStatistics();
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!runStarted || runFinished || gm == null) return;
        float now = Time.realtimeSinceStartup;
        if (now >= nextStatusTime)
        {
            Debug.Log($"【AI対戦稼働】{CompletedMatches}/{maxMatches}戦完了、" +
                $"AI1 {AI1Wins}勝 / AI2 {AI2Wins}勝 / 引分 {Draws}、" +
                $"現在 {gm.systemTurn}ターン、状態={gm.currentState}、" +
                $"phase={gm.currentPhase}、tick={gm.decisionTick}、" +
                $"状態更新から {now - lastProgressTime:F0}秒");
            nextStatusTime = now + 10f;
        }
        if (isStartingNextMatch) return;
        if (gm.decisionTick != observedTick || gm.systemTurn != observedTurn ||
            gm.currentState != observedState)
        {
            observedTick = gm.decisionTick;
            observedTurn = gm.systemTurn;
            observedState = gm.currentState;
            lastProgressTime = now;
        }
        if (gm.currentState != GameState.Finished && gm.systemTurn >= maxSystemTurns)
        {
            Debug.LogWarning($"【AI対戦】{maxSystemTurns}ターン上限で引き分け。");
            gm.FinishAsDraw();
        }
        else if (now - lastProgressTime >= stallTimeoutSeconds)
        {
            stopReason = $"進行停止: 第{CompletedMatches + 1}試合、デッキ " +
                $"{currentp1DeckId} vs {currentp2DeckId}、turn={gm.systemTurn}、" +
                $"state={gm.currentState}、phase={gm.currentPhase}、tick={gm.decisionTick}。未完了試合は集計対象外";
            runFinished = true;
            Debug.LogError(stopReason);
            PrintDetailedStatistics();
            if (ai1_New != null) ai1_New.enabled = false;
            if (ai2_New != null) ai2_New.enabled = false;
            if (ai1_Legacy != null) ai1_Legacy.enabled = false;
            if (ai2_Legacy != null) ai2_Legacy.enabled = false;
            Time.timeScale = 1f;
        }
    }

    public void StartNewMatch()
    {
        if (runFinished) return;
        isStartingNextMatch = false;

        if (gm != null)
        {
            gm.OnGameFinished -= HandleGameFinished;
        }

        // デッキの構築
        List<Card> ai1Deck = BuildAIDeck(ai1DeckIds,out currentp1DeckId);
        List<Card> ai2Deck = BuildAIDeck(ai2DeckIds,out currentp2DeckId);

        var matchKey = (currentp1DeckId,currentp2DeckId);
        if (!matchupStats.ContainsKey(matchKey))
        {
            matchupStats[matchKey] = new MatchupStat();
        }

        aiPlayer1 = new Player(ai1Deck);
        aiPlayer2 = new Player(ai2Deck);

        // 先攻後攻の決定
        bool p1GoesFirst = randomizeFirstPlayer ? UnityEngine.Random.value < 0.5f : true;
        Player first = p1GoesFirst ? aiPlayer1 : aiPlayer2;
        Player second = p1GoesFirst ? aiPlayer2 : aiPlayer1;

        gm = new GameManager(first, second);
        observedTick = gm.decisionTick;
        observedTurn = gm.systemTurn;
        observedState = gm.currentState;
        lastProgressTime = Time.realtimeSinceStartup;

        // 1P側の初期化（新旧どちらがセットされているかで分岐）
        if (ai1_New != null)
        {
            ConfigureAgentBehavior(ai1_New);
            ai1_New.Initialize(aiPlayer1, aiPlayer2, gm);
        }
        else if (ai1_Legacy != null)
        {
            ConfigureAgentBehavior(ai1_Legacy);
            ai1_Legacy.Initialize(aiPlayer1, aiPlayer2, gm);
        }

        // 2P側の初期化（新旧どちらがセットされているかで分岐）
        if (ai2_New != null)
        {
            ConfigureAgentBehavior(ai2_New);
            ai2_New.Initialize(aiPlayer2, aiPlayer1, gm);
        }
        else if (ai2_Legacy != null)
        {
            ConfigureAgentBehavior(ai2_Legacy);
            ai2_Legacy.Initialize(aiPlayer2, aiPlayer1, gm);
        }

        gm.OnGameFinished += HandleGameFinished;
    }

    private List<Card> BuildAIDeck(List<int> deckIds, out int selectedDeckId)
    {
        selectedDeckId = -1;
        if (aiDeckCatalog != null &&
            aiDeckCatalog.TryCreateRandomDeck(deckIds, out List<Card> deck, out selectedDeckId))
        {
            return deck;
        }
        return DeckManager.CreateBasicCardDeck();
    }

    // Agent（ML-Agentsの基底クラス）を受け取るように変更し、新旧両方に対応
    private void ConfigureAgentBehavior(Agent agent)
    {
        BehaviorParameters behavior = agent.GetComponent<BehaviorParameters>();
        if (behavior != null)
        {
            behavior.BehaviorType = BehaviorType.InferenceOnly;
        }
    }

    private static string GetAgentName(Agent newAgent, Agent legacyAgent)
    {
        Agent agent = newAgent != null ? newAgent : legacyAgent;
        if (agent == null) return "未設定";

        BehaviorParameters behavior = agent.GetComponent<BehaviorParameters>();
        return behavior != null && behavior.Model != null
            ? behavior.Model.name
            : agent.GetType().Name;
    }

    private void HandleGameFinished(Player winner)
    {
        if (isStartingNextMatch) return;
        isStartingNextMatch = true;

        CompletedMatches++;

        var matchKey = (currentp1DeckId, currentp2DeckId);
        matchupStats[matchKey].MatchesCount++;

        if (winner == aiPlayer1)
        {
            AI1Wins++;
            matchupStats[matchKey].P1Wins++;
        }
        else if (winner == aiPlayer2)
        {
            AI2Wins++;
            matchupStats[matchKey].P2Wins++;
        }

        if (CompletedMatches == 1 || CompletedMatches % Mathf.Max(1, progressInterval) == 0 || CompletedMatches >= maxMatches)
        {
            float winRate1 = (float)AI1Wins / CompletedMatches * 100f;
            float winRate2 = (float)AI2Wins / CompletedMatches * 100f;
            
            Debug.Log($"【AI対戦進捗】 {CompletedMatches}戦 終了\n" +
                      $"AI 1 (勝率: {winRate1:F2}%) - {AI1Wins}勝\n" +
                      $"AI 2 (勝率: {winRate2:F2}%) - {AI2Wins}勝");
            PrintDetailedStatistics();
        }

        if (CompletedMatches < maxMatches)
        {
            StartCoroutine(StartNextMatchCoroutine());
        }
        else
        {
            runFinished = true;
            stopReason = "完了";
            Debug.Log("【テスト完了】AI対戦が終了しました。");
            PrintDetailedStatistics();
            Time.timeScale = 1f; 
        }
    }

    private IEnumerator StartNextMatchCoroutine()
    {
        yield return null;
        StartNewMatch();
    }

    public class DeckStat
    {
        public int MatchesCount;
        public int Wins;
    }
    private void PrintDetailedStatistics()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("========== 詳細統計レポート ==========");
        sb.AppendLine($"状態: {stopReason}");
        sb.AppendLine($"進捗: {CompletedMatches}/{maxMatches}戦、引き分け: {Draws}戦");
        sb.AppendLine($"ターン上限: {maxSystemTurns}、進行停止検出: {stallTimeoutSeconds}秒");
        string ai1Name = GetAgentName(ai1_New, ai1_Legacy);
        string ai2Name = GetAgentName(ai2_New, ai2_Legacy);
        float overallRate1 = CompletedMatches > 0
            ? (float)AI1Wins / CompletedMatches * 100f
            : 0f;
        float overallRate2 = CompletedMatches > 0
            ? (float)AI2Wins / CompletedMatches * 100f
            : 0f;
        sb.AppendLine($"AI1: {ai1Name}");
        sb.AppendLine($"AI2: {ai2Name}");
        sb.AppendLine($"先攻ランダム化: {(randomizeFirstPlayer ? "ON" : "OFF")}");
        sb.AppendLine($"総合: AI1 {overallRate1:F2}% ({AI1Wins}勝) / " +
                      $"AI2 {overallRate2:F2}% ({AI2Wins}勝) / " +
                      $"全{CompletedMatches}戦");
        sb.AppendLine("");

        sb.AppendLine("========== 各デッキの総合勝率 ==========");
        
        Dictionary<int, DeckStat> ai1DeckStats = new Dictionary<int, DeckStat>();
        Dictionary<int, DeckStat> ai2DeckStats = new Dictionary<int, DeckStat>();
        foreach (var stat in matchupStats)
        {
            if (stat.Value.MatchesCount == 0) continue;
            int p1Deck = stat.Key.Item1;
            int p2Deck = stat.Key.Item2;
            int totalMatches = stat.Value.MatchesCount;
            int p1Wins = stat.Value.P1Wins;
            int p2Wins = stat.Value.P2Wins;

            // AI1のデッキ集計
            if (!ai1DeckStats.ContainsKey(p1Deck))
            {
                ai1DeckStats[p1Deck] = new DeckStat();
            }
            ai1DeckStats[p1Deck].MatchesCount += totalMatches;
            ai1DeckStats[p1Deck].Wins += p1Wins;

            // AI2のデッキ集計
            if (!ai2DeckStats.ContainsKey(p2Deck))
            {
                ai2DeckStats[p2Deck] = new DeckStat();
            }
            ai2DeckStats[p2Deck].MatchesCount += totalMatches;
            ai2DeckStats[p2Deck].Wins += p2Wins;
        }

        sb.AppendLine($"--- AI1 ({ai1Name}) のデッキ別成績 ---");
        foreach (var stat in ai1DeckStats)
        {
            float winRate = (float)stat.Value.Wins / stat.Value.MatchesCount * 100f;
            sb.AppendLine($"デッキID {stat.Key}: 勝率 {winRate:F2}% ({stat.Value.MatchesCount}戦 {stat.Value.Wins}勝)");
        }

        sb.AppendLine("");

        sb.AppendLine($"--- AI2 ({ai2Name}) のデッキ別成績 ---");
        foreach (var stat in ai2DeckStats)
        {
            float winRate = (float)stat.Value.Wins / stat.Value.MatchesCount * 100f;
            sb.AppendLine($"デッキID {stat.Key}: 勝率 {winRate:F2}% ({stat.Value.MatchesCount}戦 {stat.Value.Wins}勝)");
        }

        sb.AppendLine("");

        sb.AppendLine("========== デッキ組み合わせ別の詳細成績 ==========");
        foreach (var stat in matchupStats)
        {
            if (stat.Value.MatchesCount == 0) continue;
            int p1Deck = stat.Key.Item1;
            int p2Deck = stat.Key.Item2;
            int totalMatches = stat.Value.MatchesCount;
            int p1WinCount = stat.Value.P1Wins;
            int p2WinCount = stat.Value.P2Wins;

            float p1WinRate = (float)p1WinCount / totalMatches * 100f;
            float p2WinRate = (float)p2WinCount / totalMatches * 100f;

            sb.AppendLine($"【AI1: デッキ{p1Deck}】 vs 【AI2: デッキ{p2Deck}】\n" +
                    $"対戦数: {totalMatches}戦\n" +
                    $"AI1の勝率: {p1WinRate:F2}% ({p1WinCount}勝)\n" +
                    $"AI2の勝率: {p2WinRate:F2}% ({p2WinCount}勝)");
            sb.AppendLine("");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, sb.ToString());
            Debug.Log($"【対戦レポート保存】{stopReason} / {CompletedMatches}戦\n保存先: {reportPath}");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"対戦レポート保存失敗: {exception.Message}");
        }
    }
}
