using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegacyTrainingArena : MonoBehaviour
{
    [Header("対局させる2体のAgent")]
    [SerializeField] private LegacyMlAgents agentA;
    [SerializeField] private LegacyMlAgents agentB;

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

    private GameManager gm;
    private bool isStartingNextMatch;

    private void Start()
    {
        if (useSavedDecks)
        {
            DeckManager.LoadDeck();
        }
        StartNewMatch();
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

        Player playerA = new Player(deckA);
        Player playerB = new Player(deckB);

        gm = new GameManager(playerA, playerB);

        agentA.Initialize(playerA, playerB, gm);
        agentB.Initialize(playerB, playerA, gm);

        // Agentが終了報酬を処理した後、次フレームで次の対局を始める。
        gm.OnGameFinished += HandleGameFinished;

        Debug.Log(
            $"【旧方式学習】対局開始！ Agent A Deck ID:{FormatDeckId(deckAId)} " +
            $"Agent B Deck ID:{FormatDeckId(deckBId)}");
    }

    private void HandleGameFinished(Player winner)
    {
        if (isStartingNextMatch) return;
        isStartingNextMatch = true;
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
