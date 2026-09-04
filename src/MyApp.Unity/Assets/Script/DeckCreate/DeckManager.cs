using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class DeckManager
{
    public static List<int> player1Deck{get;private set;} = new List<int>();
    public static List<int> player2Deck{get;private set;} = new List<int>();
    public const int MAXDECKNUM = 40;
    public const int MAXSAMECARD = 4;
    public static void AddDeck(int wicthDeck,int cardId)
    {
        switch (wicthDeck)
        {
            case 1:
                if(player1Deck.Count >= MAXDECKNUM) break;
                if(player1Deck.Count(f=>f==cardId) < MAXSAMECARD)player1Deck.Add(cardId);
                break;
            case 2:
                if(player2Deck.Count >= MAXDECKNUM) break;
                if(player2Deck.Count(f=>f==cardId) < MAXSAMECARD)player2Deck.Add(cardId);
                break;
        }
        SaveDeck();
    }
    public static void RemoveDeck(int wicthDeck,int cardId)
    {
        switch (wicthDeck)
        {
            case 1:
                player1Deck.Remove(cardId);
                break;
            case 2:
                player2Deck.Remove(cardId);
                break;
        }
        SaveDeck();
    }
    public static void SaveDeck()
    {
        DeckData data1 = new DeckData();
        data1.deck = player1Deck;
        string json1 = JsonUtility.ToJson(data1);
        PlayerPrefs.SetString("Player1DeckSave", json1);

        DeckData data2 = new DeckData();
        data2.deck = player2Deck;
        string json2 = JsonUtility.ToJson(data2);
        PlayerPrefs.SetString("Player2DeckSave", json2);

        PlayerPrefs.Save();
        Debug.Log("デッキをオートセーブしました");
    }
    public static void LoadDeck()
    {
        if (PlayerPrefs.HasKey("Player1DeckSave"))
        {
            string json1 = PlayerPrefs.GetString("Player1DeckSave");
            DeckData data1 = JsonUtility.FromJson<DeckData>(json1);
            player1Deck = data1.deck;
        }

        if (PlayerPrefs.HasKey("Player2DeckSave"))
        {
            string json2 = PlayerPrefs.GetString("Player2DeckSave");
            DeckData data2 = JsonUtility.FromJson<DeckData>(json2);
            player2Deck = data2.deck;
        }
        Debug.Log("保存されたデッキをロードしました");
    }
    public static int[] GetDeckArrayForNetwork(int whichDeck)
    {
        List<int> targetDeck = (whichDeck == 1) ? player1Deck : player2Deck;
        
        return targetDeck.ToArray(); 
    }
    public static List<Card> CreateBasicCardDeck()
    {
        List<Card> deck = new List<Card>();
        for (int i = 1; i < 11; i++)
        {
            for(int j = 0;j < 4; j++)
            {
                Card c = new Card();
                c.SettingBasicCard(i);
                deck.Add(c);
            }
        }
        return deck;
    }
}