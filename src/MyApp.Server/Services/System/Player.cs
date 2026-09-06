using System.Collections.Generic;
using System.Linq;
using System;

public class Player
{
    public int turn = 0;
    public int maxMemory = 20; //使用可能メモリ
    public int usableMemory = 0; //現在使えるメモリ
    public int fieldCost = 0;
    public int usedMemory = 0;

    public List<Card> hand = new List<Card>();
    public List<Card> deck = new List<Card>();
    public List<Card> garbage = new List<Card>();
    public List<Card> field = new List<Card>();
    public GameManager gm;
    public Random rand;

    public event Action<Card> OnFailSafeTriggered;
    public event Action<Card> OnCardSpawned;
    public void TriggerCardSpawned(Card spawnedCard)
    {
        OnCardSpawned?.Invoke(spawnedCard);
    }
    public Player(List<Card> Deck)
    {
        deck.AddRange(Deck);
        foreach(Card c in Deck)
        {
            c.player = this;
        }
        rand = new Random();
    }
    public bool Marigan(List<Card> cards)
    {
        if(cards == null||cards.Any(c => c == null)||cards.Distinct().Count() != cards.Count||cards.Any(c => c.player != this || !hand.Contains(c)))
        {
            return false;
        }
        Draw(cards.Count);
        foreach(var c in cards)
        {
            hand.Remove(c);
            deck.Add(c);
        }
        Shuffle();
        return true;
    }
    public void DirectAttack(Player enemy,Card attacker)
    {
        maxMemory += attacker.Attack;
        enemy.maxMemory -= attacker.Attack;
    }

    private Card RandomSelect(List<Card> target)
    {
        if(target == null || target.Count == 0)return null;
        int size = target.Count;
        int rnd = rand.Next(0,size);
        return target[rnd];
    }

    public void Shuffle()
    {
        for(var i = deck.Count - 1;i > 0;i--){
            var j = rand.Next(0,i+1);
            var temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    public bool Draw()
    {
        if(deck.Count <= 0)
        {
            return true;
        }

        var top = deck.Count-1;
        var c = deck[top];
        deck.RemoveAt(top);
        if(hand.Count >= 8)
        {
            garbage.Add(c);
            gm.WriteLog(LogType.DrawGabage,null,new List<Card>([c]),1);
        }
        else
        {
            hand.Add(c);
            gm.WriteLog(LogType.DrawHand,null,new List<Card>([c]),1);
        }
        return false;
    }

    public bool Draw(int num)
    {
        List<Card> handCards = new List<Card>();
        List<Card> garbageCards = new List<Card>();
        for(int i = 0;i < num; i++)
        {
            if(deck.Count <= 0)
            {
                return true;
            }
            var top = deck.Count-1;
            var c = deck[top];
            deck.RemoveAt(top);
            if(hand.Count >= 8)
            {
                garbage.Add(c);
                garbageCards.Add(c);

            }
            else
            {
                hand.Add(c);
                handCards.Add(c);
            }
        }
        
        if(garbageCards.Count > 0)
            gm.WriteLog(LogType.DrawHand,null,handCards,1);
        if(garbageCards.Count > 0)
            gm.WriteLog(LogType.DrawGabage,null,garbageCards,garbageCards.Count);
            
        return false;
    }

    public void DestoryField(Player enemy,List<Card> target,bool isStartPhase = false)
    {
        if(target == null)
        {
            return;
        }
        int destroyedCount = 0;
        foreach(var c in target.Where(c=>c != null).Distinct().ToList())
        {
            if(c.player == null || !c.player.field.Contains(c)) continue;
            if(c.isDaemon == true)
            {
                c.player.field.Remove(c);
                c.ExecuteDestructor(c.player==this ? enemy: this);
                c.player.fieldCost -= c.Cost;
            }
            else
            {
                c.player.garbage.Add(c);
                c.player.field.Remove(c);
                c.ExecuteDestructor(c.player==this ? enemy: this);
                c.player.fieldCost -= c.Cost;
                c.player.maxMemory -= c.Cost;
                maxMemory += c.Cost;
            }
            //変更されたステータスの修正
            c.Cost -= c.ChangeCost;
            c.Attack -= c.ChangeAttack;
            c.Hp -= c.ChangeHp;

            c.ChangeCost = 0;
            c.ChangeAttack = 0;
            c.ChangeHp = 0;
            c.isDaemon = c.Daemon;
            c.isEncrypted = c.Encrypted;
            c.isImmediate = c.Immediate;
            c.isProxy = c.Proxy;
            c.isSandBox = c.SandBox;
            c.isSegfault = c.Segfault;
            destroyedCount++;
        }
        if (isStartPhase && destroyedCount > 0)
        {
            DrawG();
        }
    }

    public void DoFailSafe(Player enemy,Card c)
    {
        c.ChangeCost += -c.Cost;
        c.Cost = 0;
        field.Add(c);
        deck.Remove(c);
        c.OnPlay();
        c.FailSafe(enemy);
        //カードのフェイルセーフ側でコンストラクタを呼び出す。
        OnFailSafeTriggered?.Invoke(c);
    }

    public void DrawG()
    {
        if(garbage.Count <= 0)return;
        var c = RandomSelect(garbage);
        if(hand.Count >= 8)
        {
            return;
        }
        garbage.Remove(c);
        hand.Add(c);
    }
    public bool PlayFeild(Card c, bool consumeUsableMemory = true)
    {
        if(c== null || field.Contains(c) || gm.currentScope == c) return false;
        if(!hand.Contains(c)&&!deck.Contains(c)&&!garbage.Contains(c))return false;
        c.wasPlayedFromGarbage = garbage.Contains(c);
        if(c.Type == Card.CardType.Scope)
        {
            if(hand.Contains(c))
                hand.Remove(c);
            else if(deck.Contains(c))
                deck.Remove(c);
            else if(garbage.Contains(c))
                garbage.Remove(c);
            if (gm.currentScope != null)
            {
                Card oldScope = gm.currentScope;
                Player oldOwner = oldScope.player;
                Player oldEnemy = oldOwner == this ? gm.notrun : this;

                oldScope.ExecuteDestructor(oldEnemy);
                if(!oldOwner.garbage.Contains(oldScope))
                    oldOwner.garbage.Add(oldScope);
                oldScope.Cost -= oldScope.ChangeCost;
                oldScope.ChangeCost = 0;
            }
            gm.currentScope = c;
        }
        else
        {
            if (hand.Contains(c))
            {
                hand.Remove(c);   
                field.Add(c);
                fieldCost += c.Cost;
            }
            else if(deck.Contains(c)){
                deck.Remove(c);
                field.Add(c);
                fieldCost += c.Cost;
            }
            else if(garbage.Contains(c)){
                garbage.Remove(c);
                field.Add(c);
                fieldCost += c.Cost;
            }
        }
        if(consumeUsableMemory)
        {
            usedMemory += c.Cost;
        }

        return true;
    }  
    //カードIDからインスタンスを作成する。
    public static List<Card> ChangeCard(int[] deckData)
    {
        List<Card> deck = new List<Card>();
        foreach(int i in deckData)
        {
            Card c = Card.CreateCardInstance(i);
            if(c==null)continue;
            deck.Add(c);
        }
        return deck;
    }
    public static List<Card> ChangeCard(CardData[] deckData)
    {
        List<Card> deck = new List<Card>();
        foreach(CardData data in deckData)
        {
            Card c = Card.CreateCardInstance(data.id);
            if(c==null)continue;
            deck.Add(c);
            c.Attack = data.atk;
            c.Hp = data.hp;
            c.Cost = data.cost;
            c.isCanAttack = data.canAttackNow;
        }
        return deck;
    }
    //指定された範囲からカードIdの一致するインスタンスを探す。
    public static List<Card> SearchCard(int[] Ids,List<Card> cards)
    {
        List<int> lost = new List<int>();
        List<Card> get = new List<Card>();
        List<Card> target = cards.ToList();
        foreach(int i in Ids)
        {
            string className = Card.GetCardClassName(i);
            if(className == null)
            {
                lost.Add(i);
                continue;
            }
            foreach(Card c in target)
            {
                if(c.GetType().Name == className)
                {
                    get.Add(c);
                    target.Remove(c);
                    break;
                }
            }
        }
        return get;
    } 
    
}
