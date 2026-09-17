using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AIDeckCatalog", menuName = "CardGame/AI Deck Catalog")]
public class AIDeckCatalog : ScriptableObject
{
    public const int FirstCardId = 0;
    public const int LastCardId = 44;

    [Serializable]
    public class CardAmount
    {
        public int cardId;
        [Range(0, DeckManager.MAXSAMECARD)] public int amount;
    }

    [Serializable]
    public class DeckEntry
    {
        [Tooltip("このデッキを指定するための一意なID")]
        public int deckId;

        [Tooltip("カードごとの投入枚数（各0～4枚）")]
        public List<CardAmount> cardAmounts = new List<CardAmount>();

        // 旧カタログをカード別枚数へ自動移行するために残す。
        [HideInInspector] public List<int> cardIds = new List<int>();

        public int TotalCardCount
        {
            get
            {
                int total = 0;
                foreach(CardAmount cardAmount in cardAmounts)
                {
                    if(cardAmount != null)total += cardAmount.amount;
                }
                return total;
            }
        }

        public List<int> BuildCardIds()
        {
            List<int> ids = new List<int>(TotalCardCount);
            foreach(CardAmount cardAmount in cardAmounts)
            {
                if(cardAmount == null)continue;
                for(int i = 0; i < cardAmount.amount; i++)ids.Add(cardAmount.cardId);
            }
            return ids;
        }
    }

    [SerializeField] private List<DeckEntry> decks = new List<DeckEntry>();

    public bool TryCreateRandomDeck(
        IReadOnlyList<int> allowedDeckIds,
        out List<Card> deck,
        out int selectedDeckId)
    {
        List<DeckEntry> candidates = new List<DeckEntry>();
        bool useAllRegisteredDecks = allowedDeckIds == null || allowedDeckIds.Count == 0;

        foreach(DeckEntry entry in decks)
        {
            if(entry == null || entry.cardAmounts == null)continue;
            if(!useAllRegisteredDecks && !ContainsId(allowedDeckIds, entry.deckId))continue;
            if(entry.TotalCardCount != DeckManager.MAXDECKNUM)continue;

            List<int> cardIds = entry.BuildCardIds();
            List<Card> cards = Player.ChangeCard(cardIds.ToArray());
            if(cards.Count == DeckManager.MAXDECKNUM)candidates.Add(entry);
        }

        if(candidates.Count == 0)
        {
            deck = null;
            selectedDeckId = -1;
            return false;
        }

        DeckEntry selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        deck = Player.ChangeCard(selected.BuildCardIds().ToArray());
        selectedDeckId = selected.deckId;
        return true;
    }

    private static bool ContainsId(IReadOnlyList<int> ids, int targetId)
    {
        for(int i = 0; i < ids.Count; i++)
        {
            if(ids[i] == targetId)return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        HashSet<int> registeredIds = new HashSet<int>();
        foreach(DeckEntry entry in decks)
        {
            if(entry == null)continue;
            EnsureCardList(entry);
            if(!registeredIds.Add(entry.deckId))
            {
                Debug.LogWarning($"AIDeckCatalogに重複したデッキIDがあります: {entry.deckId}", this);
            }
        }
    }

    private static void EnsureCardList(DeckEntry entry)
    {
        Dictionary<int, int> amounts = new Dictionary<int, int>();
        if(entry.cardAmounts != null)
        {
            foreach(CardAmount cardAmount in entry.cardAmounts)
            {
                if(cardAmount == null || cardAmount.cardId < FirstCardId ||
                    cardAmount.cardId > LastCardId)continue;
                amounts[cardAmount.cardId] = Mathf.Clamp(
                    cardAmount.amount, 0, DeckManager.MAXSAMECARD);
            }
        }

        if(entry.cardIds != null)
        {
            foreach(int cardId in entry.cardIds)
            {
                if(cardId < FirstCardId || cardId > LastCardId)continue;
                amounts.TryGetValue(cardId, out int amount);
                amounts[cardId] = Mathf.Min(amount + 1, DeckManager.MAXSAMECARD);
            }
            entry.cardIds.Clear();
        }

        entry.cardAmounts = new List<CardAmount>();
        for(int cardId = FirstCardId; cardId <= LastCardId; cardId++)
        {
            amounts.TryGetValue(cardId, out int amount);
            entry.cardAmounts.Add(new CardAmount(){cardId = cardId, amount = amount});
        }
    }
#endif
}
