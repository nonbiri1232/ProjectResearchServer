
using System;
using System.Collections.Generic;

public enum LogType
{
    TurnStart,
    PlayCard,
    Attack,
    SelfDestory,
    FailSafe,
    Marigan
}

public struct CardSnapshot
{
    public int cardId;
    public int cost;
    public int atk;
    public int hp;
}

public class PlayLog
{
    public int turnNumber;
    public bool isPlayer1;
    public LogType type;
    public CardSnapshot cardName;
    public CardSnapshot[] targetName;

    public PlayLog(int turnNumber,bool isPlayer1,LogType type,CardSnapshot cardName = default,CardSnapshot[] targetName = null)
    {
        this.turnNumber = turnNumber;
        this.isPlayer1 = isPlayer1;
        this.type = type;
        this.cardName = cardName;
        this.targetName = targetName;
    }
    public PlayLog(int turnNumber,bool isPlayer1,LogType type,CardSnapshot[] targetName)
    {
        this.turnNumber = turnNumber;
        this.isPlayer1 = isPlayer1;
        this.type = type;
        this.targetName = targetName;
    }
    public static CardSnapshot PackageData(Card target)
    {
        CardSnapshot cardData;
        cardData.cardId = Card.GetCardId(target);
        cardData.atk = target.Attack;
        cardData.cost = target.Cost;
        cardData.hp = target.Hp;
        return cardData;
    }
    public static CardSnapshot[] PackageData(List<Card> target)
    {
        List<CardSnapshot> cardDatas = new List<CardSnapshot>();
        foreach(Card c in target)
        {    
            CardSnapshot cardData;
            cardData.cardId = Card.GetCardId(c);
            cardData.atk = c.Attack;
            cardData.cost = c.Cost;
            cardData.hp = c.Hp;
            cardDatas.Add(cardData);
        }
        return cardDatas.ToArray();
    }
}