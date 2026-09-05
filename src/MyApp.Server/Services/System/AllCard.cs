using System.Collections.Generic;

public class SledOverClock : Card
{
    public SledOverClock()
    {
        Cost = 3;
        Attack = 5;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy,List<Card> target)
    {
        if(!cr.effectOnAttack.Contains(this))cr.effectOnAttack.Add(this);
        if(!cr.effectOnPlay.Contains(this))cr.effectOnPlay.Add(this);
        foreach(Card c in player.hand)
        {
            if(c.Cost > 2)
            {
                c.ChangeCost -= 2;
                c.Cost -= 2;
            }
            else
            {
                c.ChangeCost -= c.Cost;
                c.Cost -= c.Cost;
            }
        }
        isImmediate = true;
    }
    public override void StartPhase(Player Enemy)
    {
        if(!cr.effectOnAttack.Contains(this))cr.effectOnAttack.Add(this);
        if(!cr.effectOnPlay.Contains(this))cr.effectOnPlay.Add(this);
    }
    public override void EndPhase(Player Enemy)
    {
        cr.effectOnAttack.Remove(this);
        cr.effectOnPlay.Remove(this);
    }
    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        cr.effectOnAttack.Remove(this);
        cr.effectOnPlay.Remove(this);
    }

    public override void CrestOnPlay(Player Enemy, Card target = null)
    {
        target.isImmediate = true;
    }
    public override void CrestOnAttack(Player Enemy, List<Card> target = null)
    {
        target[0].isSandBox = true;
    }
}
public class IncrementProcess : Card
{
    public IncrementProcess()
    {
        Cost = 1;
        Attack =1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select(where.selfField,1);
    }
    public override bool ValidateTargets(Player move, Player enemy, List<Card> targets)
    {
        return targets != null && targets.Count == 1 &&
               targets[0] != this && targets[0].Type == CardType.Object;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Card actualTarget = target != null && target.Count == 1
            ? target[0]
            : RandomSelect(player.field.FindAll(c=>c != this && c.Type == CardType.Object));
        if(actualTarget != null){
            actualTarget.Attack += 1;
            actualTarget.ChangeAttack += 1;
            actualTarget.Hp += 1;
            actualTarget.ChangeHp += 1;
        }
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        player.Draw();
    }

}
public class ClockDownBot : Card
{
    public ClockDownBot()
    {
        Cost = 1;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select(where.enemyField,1);
    }

    public override bool ValidateTargets(Player move, Player enemy, List<Card> targets)
    {
        return targets != null && targets.Count == 1 &&
               targets[0].Type == CardType.Object;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Card actualTarget = target != null && target.Count == 1
            ? target[0]
            : RandomSelect(Enemy.field.FindAll(c=>c.Type == CardType.Object));
        if(actualTarget != null)
        {
            if(actualTarget.Attack >= 3)
            {
                actualTarget.Attack -= 3;
                actualTarget.ChangeAttack -= 3;
            }
            else
            {
                actualTarget.ChangeAttack -= actualTarget.Attack;
                actualTarget.Attack = 0;
            }
        }
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        player.Draw();
    }
}

public class ParallelCompilation : Card
{
    public ParallelCompilation()
    {
        Cost = 5;
        Attack = 3;
        Hp = 3;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        foreach(var c in player.field)
        {
            c.Attack += 3;
            c.ChangeAttack += 3;
            c.Hp += 3;
            c.ChangeHp += 3;
            c.isImmediate = true;
        }
    }
}
public class PoisonPoint : Card
{
    public PoisonPoint()
    {
        Cost = 2;
        Attack = 0;
        Hp = 2;
        Type = CardType.Object;
        select = new Select(where.selfField,1);
    }
    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Card actualTarget = null;
        if(target != null && target.Count > 0)
        {
            actualTarget = target[0];
        }
        else
        {
            if(player.field.Count > 0)
            {
                actualTarget = RandomSelect(player.field);
            }
        }
        if(actualTarget != null){
            actualTarget.isSegfault = true;
            actualTarget.isDaemon = true;
        }
        isSegfault = true;
        isDaemon = true;
    }
}
public class UnSafeArea : Card
{
    public UnSafeArea()
    {
        Cost = 3;
        Type = CardType.Scope;
        select = new Select();
    }

    public override void ScopeEffectOnPlay(Player pl,Card target = null)
    {
        if(target != null){
            target.isImmediate = true;
        }
    }
}

public class Master : Card
{
    public Master()
    {
        isProxy = true;
        isSegfault = true;

        Cost = 7;
        Attack = 1;
        Hp = 10;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        List<Card> randaomCard = new List<Card>(); 
        foreach(var c in player.deck)
        {
            if(c.Type == CardType.Object && c.Cost >= 8)
            {
                randaomCard.Add(c);
            }
        }
        if(randaomCard.Count >= 2)
        {
            var c1 = RandomSelect(randaomCard);
            randaomCard.Remove(c1);
            var c2 = RandomSelect(randaomCard);
            randaomCard.Clear();
            c1.ChangeCost -= c1.Cost;
            c2.ChangeCost -= c2.Cost;
            c1.Cost = 0;
            c2.Cost = 0;
            c1.isImmediate = true;
            c2.isImmediate = true;
            CardImplementationUtilities.MaterializeWithoutUsableMemory(player, Enemy, c1);
            CardImplementationUtilities.MaterializeWithoutUsableMemory(player, Enemy, c2);
        }
        else if(randaomCard.Count == 1)
        {
            var c1 = randaomCard[0];
            c1.ChangeCost -= c1.Cost;
            c1.Cost = 0;
            c1.isImmediate = true;
            CardImplementationUtilities.MaterializeWithoutUsableMemory(player, Enemy, c1);
        }
    }
}
public class Raid10 : Card
{
    public Raid10()
    {
        Cost = 8;
        Attack = 10;
        Hp = 5;
        isSandBox = true;
        attackTimes = 2;
        select = new Select();
        Type = CardType.Object;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        if(player.field.Count > 0){
            var c1 = RandomSelect(player.field);
            c1.Attack += 5;
            c1.Hp += 5;
            c1.ChangeAttack += 5;
            c1.ChangeHp += 5;
        }
    }

    public override void StartPhase(Player Enemy)
    {
        isSandBox = true;
    }
}
public class RmRf : Card
{
    public RmRf()
    {
        Cost = 20;
        Attack = 10;
        Hp = 3;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        List<Card> targetList = Enemy.field.FindAll(c=>c.Type == CardType.Object);
        player.DestoryField(Enemy, targetList);
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        Card c = RandomSelect(Enemy.field.FindAll(card=>card.Type == CardType.Object));
        if (c != null)
        {
            List<Card> list = new List<Card>(){c};
            player.DestoryField(Enemy,list);
        }
    }
}
public class Paging : Card
{
    public Paging()
    {
        Cost = 2;
        Type = CardType.Method;
        select = new Select(where.hand,1);
    }

    public override bool ValidateTargets(Player move, Player enemy, List<Card> targets)
    {
        return targets != null && targets.Count == 1 && targets[0] != this;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        if(target == null || target.Count != 1)return;
        player.hand.Remove(target[0]);
        player.deck.Add(target[0]);
        player.Shuffle();
        player.Draw(2);
    }
}
public class BackGroundMiner : Card
{
    public BackGroundMiner()
    {
        isDaemon = true;
        isProxy = true;

        Cost = 3;
        Attack = 2;
        Hp = 2;
        Type = CardType.Object;
        select = new Select();
    }
    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Enemy.maxMemory -= 2;
        player.maxMemory += 2;
    }
}
public class SystemFreeze : Card
{
    public SystemFreeze()
    {   
        isProxy = true;
        Cost = 5;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        List<Card> targetList = Enemy.field.FindAll(c=>c.Type == CardType.Object);
        player.DestoryField(Enemy, targetList);
    }
}
public class CarnelPanicZero : Card
{
    public CarnelPanicZero()
    {
        Cost = 1;
        Attack = 20;
        Hp = 20;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        if(player.maxMemory != 1 && player.field.Contains(this))
        {
            player.field.Remove(this);
            player.fieldCost -= this.Cost;
            player.deck.Add(this);
            player.Shuffle();
        }
    }
    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        player.garbage.Remove(this);
    }
    public override bool IsFailSafe()
    {
        if(player.maxMemory > 0 && player.maxMemory <= 5)return true;
        return false;
    }
    public override void FailSafe(Player Enemy, List<Card> target = null)
    {
        bool activatedAtFailSafeOne = player.maxMemory == 1;

        if(player.maxMemory > 0 && player.maxMemory <= 5)
        {
            if(Enemy.field.Count > 0){
                var c = RandomSelect(Enemy.field);
                c.isCanAttack = false;
            }
        }
        if(player.maxMemory > 0 && player.maxMemory <= 3)
        {
            if(player.hand.Count > 0)
            {
                foreach(var c in player.hand)
                {
                    if(c.Cost >= 5)
                    {
                        c.ChangeCost -= 5;
                        c.Cost -= 5;
                    }
                    else
                    {
                        c.ChangeCost -= c.Cost;
                        c.Cost -= c.Cost;
                    }
                }
            }
        }
        if(player.maxMemory == 1)
        {
            List<Card> targetList = new List<Card>(Enemy.field);
            player.DestoryField(Enemy, targetList);
        }
        if(!activatedAtFailSafeOne)
        {
            this.Constructor(Enemy,target);
        }
    }
}
public class AllDelete : Card
{
    public AllDelete()
    {
        isAssert = true;
        Assert = 10;

        Cost = 2;
        Type = CardType.Method;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        List<Card> allDelete = new List<Card>();
        if(player.field.Count > 0)
        {
            allDelete.AddRange(player.field);
        }
        if(Enemy.field.Count > 0)
        {
            allDelete.AddRange(Enemy.field);
        }
        player.DestoryField(Enemy,allDelete);
    }
}
public static class CardImplementationUtilities
{
    public static bool MaterializeWithoutUsableMemory(Player owner, Player enemy, Card card)
    {
        if(owner == null || enemy == null || card == null)return false;
        if(!owner.PlayFeild(card, false))return false;

        if(owner.gm.currentScope != null)
        {
            owner.gm.currentScope.ScopeEffectOnPlay(enemy, card);
        }
        card.cr.OnPlay(card);
        card.Constructor(enemy);
        card.OnPlay();

        owner.TriggerCardSpawned(card);
        return true;
    }

    public static int TransferMemory(Player from, Player to, int amount)
    {
        int transferable = from.maxMemory - 1;
        if(transferable < 0)transferable = 0;
        int actual = transferable < amount ? transferable : amount;
        from.maxMemory -= actual;
        to.maxMemory += actual;
        return actual;
    }

    public static void DamageObject(Player actor, Player owner, Card target, int damage)
    {
        if(target == null)return;
        target.Hp -= damage;
        target.ChangeHp -= damage;
        if(target.Hp <= 0)
        {
            actor.DestoryField(owner, new List<Card>(){target});
        }
    }

    public static void DamageAllObjects(Player actor, Player owner, int damage)
    {
        List<Card> targets = new List<Card>(owner.field);
        List<Card> destroyed = new List<Card>();
        foreach(Card c in targets)
        {
            if(c.Type != Card.CardType.Object)continue;
            c.Hp -= damage;
            c.ChangeHp -= damage;
            if(c.Hp <= 0)destroyed.Add(c);
        }
        if(destroyed.Count > 0)actor.DestoryField(owner, destroyed);
    }
}

public class SafeModeOverdrive : Card
{
    public SafeModeOverdrive()
    {
        Cost = 1;
        Attack = 9;
        Hp = 9;
        Type = CardType.Object;
        isAssert = true;
        Assert = 5;
        attackTimes = 2;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        if(!cr.effectOnAttack.Contains(this))cr.effectOnAttack.Add(this);
    }

    public override void StartPhase(Player Enemy)
    {
        if(!cr.effectOnAttack.Contains(this))cr.effectOnAttack.Add(this);
    }

    public override void EndPhase(Player Enemy)
    {
        cr.effectOnAttack.Remove(this);
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        cr.effectOnAttack.Remove(this);
    }

    public override void CrestOnAttack(Player Enemy, List<Card> target = null)
    {
        if(target != null && target.Count > 0 && target[0] == this)
        {
            isSandBox = true;
        }
    }
}

public class ForcedCrashTest : Card
{
    public ForcedCrashTest()
    {
        Cost = 3;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        isAssert = true;
        Assert = 10;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        const int forcedFailSafeMemory = 1;
        int originalMemory = player.maxMemory;

        try
        {
            player.maxMemory = forcedFailSafeMemory;
            Card selected = null;
            foreach(Card card in player.deck)
            {
                if(card.IsFailSafe())
                {
                    selected = card;
                    break;
                }
            }

            if(selected != null)
            {
                player.DoFailSafe(Enemy, selected);
            }
        }
        finally
        {
            int failSafeMemoryDelta = player.maxMemory - forcedFailSafeMemory;
            player.maxMemory = originalMemory + failSafeMemoryDelta;
        }
    }
}

public class IllegalResourceSale : Card
{
    public IllegalResourceSale()
    {
        Cost = 2;
        Attack = 3;
        Hp = 3;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        CardImplementationUtilities.TransferMemory(player, Enemy, 5);
        player.Draw(3);
    }
}

public class ForcedDebugMode : Card
{
    public ForcedDebugMode()
    {
        Cost = 4;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        CardImplementationUtilities.TransferMemory(player, Enemy, 2);
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        CardImplementationUtilities.TransferMemory(Enemy, player, 2);
    }
}

public class RansomwareInfection : Card
{
    public RansomwareInfection()
    {
        Cost = 5;
        Attack = 2;
        Hp = 2;
        Type = CardType.Object;
        isDaemon = true;
        isEncrypted = true;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Player infectionOwner = player;

        foreach (Card infectedCard in Enemy.field)
        {
            infectedCard.isCanAttack = false;

            infectedCard.AddDestructorEffect(
                (enemyOfDestroyedCard, ignoredTargets) =>
                {
                    CardImplementationUtilities.TransferMemory(infectedCard.player,infectionOwner,infectedCard.Cost);
                });
        }
    }
}

public class LeechProcess : Card
{
    public LeechProcess()
    {
        Cost = 2;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        isDaemon = true;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        CardImplementationUtilities.TransferMemory(Enemy, player, 3);
    }

    public override void OpponentEndPhase(Player Enemy)
    {
        CardImplementationUtilities.TransferMemory(Enemy, player, 1);
    }
}

public class DDoSArea : Card
{
    public DDoSArea()
    {
        Cost = 5;
        Type = CardType.Scope;
        select = new Select();
    }

    public override void ScopeEffectOnPlay(Player pl, Card target = null)
    {
        if(target == null || target == this)return;

        Card materializedCard = target;
        materializedCard.AddDestructorEffect(
            (enemyOfDestroyedCard, ignoredTargets) =>
            {
                Player owner = materializedCard.player;
                if(owner == null)return;

                Card discarded = RandomSelect(owner.hand);
                if(discarded == null)return;

                owner.hand.Remove(discarded);
                owner.garbage.Add(discarded);
            });
    }
}

public class MultiEncryptionProtocol : Card
{
    public MultiEncryptionProtocol()
    {
        Cost = 3;
        Type = CardType.Method;
        select = new Select();
    }

    public override void EndPhase(Player Enemy)
    {
        foreach(Card c in player.field)
        {
            c.isEncrypted = true;
        }
    }
}

public class TimedLogicBomb : Card
{
    public TimedLogicBomb()
    {
        Cost = 5;
        Attack = 0;
        Hp = 4;
        Type = CardType.Object;
        isDaemon = true;
        isEncrypted = true;
        select = new Select(where.enemyField, 1);
    }

    public override bool ValidateTargets(Player move, Player enemy, List<Card> targets)
    {
        return targets != null && targets.Count == 1 &&
               targets[0].Type == CardType.Object;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        if(target == null || target.Count != 1 || target[0] == null)return;
        Card infectedCard = target[0];
        Player infectionOwner = player;

        infectedCard.AddDestructorEffect(
            (enemyOfDestroyedCard, ignoredTargets) =>
            {
                CardImplementationUtilities.TransferMemory(infectedCard.player,infectionOwner,infectedCard.Cost);
            });
    }

    public override void EndPhase(Player Enemy)
    {
        CardImplementationUtilities.DamageAllObjects(player, Enemy, 5);
    }
}

public class ForgedFile : Card
{
    public ForgedFile()
    {
        Cost = 1;
        Attack = 0;
        Hp = 1;
        Type = CardType.Object;
        isDaemon = true;
        isCanAttack = false;
        attackTimes = 0;
        select = new Select();
    }


    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        Card discarded = RandomSelect(player.hand);
        if(discarded != null)
        {
            player.hand.Remove(discarded);
        }
    }
}

public class TrojanHorse : Card
{
    public TrojanHorse()
    {
        Cost = 5;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        for(int i = 0; i < 5; i++)
        {
            if(Enemy.maxMemory - Enemy.fieldCost < 1)break;
            Card token = new ForgedFile();
            token.player = Enemy;
            token.cr = cr;
            Enemy.field.Add(token);
            Enemy.fieldCost += token.Cost;
            if(player.gm.currentScope != null)
            {
                player.gm.currentScope.ScopeEffectOnPlay(player,token);
            }
            cr.OnPlay(token);
            token.OnPlay();
            Enemy.TriggerCardSpawned(token);
        }
    }
}

public class MemoryDumpRestore : Card
{
    public MemoryDumpRestore()
    {
        Cost = 7;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        isProxy = true;
        isDaemon = true;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        cr.effectOnPlay.Add(this);

        while(true)
        {
            int availableMemory = player.maxMemory - player.fieldCost;
            List<Card> candidates = new List<Card>();
            int highestCost = -1;

            foreach(Card c in player.garbage)
            {
                if(c.Type != CardType.Object || c.Cost > availableMemory)continue;

                if(c.Cost > highestCost)
                {
                    highestCost = c.Cost;
                    candidates.Clear();
                    candidates.Add(c);
                }
                else if(c.Cost == highestCost)
                {
                    candidates.Add(c);
                }
            }

            if(candidates.Count == 0)break;

            Card revived = RandomSelect(candidates);
            if(revived is ZombieProcess)revived.isDaemon = true;
            if(!CardImplementationUtilities.MaterializeWithoutUsableMemory(
                player, Enemy, revived))break;
        }
    }

    public override void StartPhase(Player Enemy)
    {
        cr.effectOnPlay.Add(this);
    }

    public override void EndPhase(Player Enemy)
    {
        cr.effectOnPlay.Remove(this);
    }

    public override void CrestOnPlay(Player Enemy, Card target = null)
    {
        if(target != null && target.Type == CardType.Object)target.isImmediate = true;
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        cr.effectOnPlay.Remove(this);
        foreach(Card c in player.field)c.isDaemon = true;
    }
}

public class CoreDumpProcess : Card
{
    public CoreDumpProcess()
    {
        Cost = 1;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    private void SendRandomDeckCardToGarbage(bool objectOnly)
    {
        List<Card> candidates = new List<Card>();
        foreach(Card c in player.deck)
        {
            if(!objectOnly || c.Type == CardType.Object)candidates.Add(c);
        }
        Card selected = RandomSelect(candidates);
        if(selected != null)
        {
            player.deck.Remove(selected);
            player.garbage.Add(selected);
        }
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        for(int i = 0; i < 3; i++)SendRandomDeckCardToGarbage(true);
        player.Draw();
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        SendRandomDeckCardToGarbage(false);
    }
}

public class ZombieProcess : Card
{
    public ZombieProcess()
    {
        Cost = 2;
        Attack = 2;
        Hp = 2;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        if(wasPlayedFromGarbage)isDaemon = true;
        player.Draw();
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        CardImplementationUtilities.TransferMemory(Enemy, player, 1);
    }
}

public class DeepArchive : Card
{
    public DeepArchive()
    {
        Cost = 3;
        Type = CardType.Scope;
        select = new Select();
    }

    public override bool AddCost(Player Enemy, List<Card> target = null)
    {
        return player.garbage.Count >= 10;
    }

    public override void EndPhase(Player Enemy)
    {
        if(player.garbage.Count < 10)return;
        Card selected = RandomSelect(player.deck);
        if(selected != null)
        {
            player.deck.Remove(selected);
            player.garbage.Add(selected);
        }
    }
}

public class RestoreMeister : Card
{
    public RestoreMeister()
    {
        Cost = 5;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        int totalOriginalCost = 0;
        for(int i = 0; i < 2; i++)
        {
            int highestCost = -1;
            List<Card> candidates = new List<Card>();
            foreach(Card c in player.garbage)
            {
                if(c.Type != CardType.Object)continue;
                if(c.Cost > highestCost)
                {
                    highestCost = c.Cost;
                    candidates.Clear();
                    candidates.Add(c);
                }
                else if(c.Cost == highestCost)candidates.Add(c);
            }
            if(candidates.Count == 0)break;

            Card revived = RandomSelect(candidates);
            int originalCost = revived.Cost;
            bool originalDaemon = revived.isDaemon;
            revived.ChangeCost -= originalCost;
            revived.Cost = 0;
            if(revived is ZombieProcess)revived.isDaemon = true;

            bool played = CardImplementationUtilities.MaterializeWithoutUsableMemory(
                player, Enemy, revived);

            if(!played)
            {
                revived.Cost = originalCost;
                revived.ChangeCost += originalCost;
                revived.isDaemon = originalDaemon;
                break;
            }

            totalOriginalCost += originalCost;
        }
        Attack += totalOriginalCost;
        ChangeAttack += totalOriginalCost;
        Hp += totalOriginalCost;
        ChangeHp += totalOriginalCost;
    }
}

public class FakeHoneypot : Card
{
    public FakeHoneypot()
    {
        Cost = 1;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        player.Draw();
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        CardImplementationUtilities.TransferMemory(Enemy, player, 5);
    }
}

public class PingBot : Card
{
    public PingBot()
    {
        Cost = 1;
        Attack = 1;
        Hp = 1;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        player.Draw();
    }
}

public class Firewall : Card
{
    public Firewall()
    {
        Cost = 2;
        Attack = 1;
        Hp = 3;
        Type = CardType.Object;
        isProxy = true;
        select = new Select();
    }
}

public class DebugProcess : Card
{
    public DebugProcess()
    {
        Cost = 2;
        Attack = 1;
        Hp = 2;
        Type = CardType.Object;
        isSegfault = true;
        select = new Select();
    }
}

public class BackupServer : Card
{
    public BackupServer()
    {
        Cost = 3;
        Attack = 2;
        Hp = 4;
        Type = CardType.Object;
        isProxy = true;
        isDaemon = true;
        select = new Select();
    }
}

public class GarbageShredder : Card
{
    public GarbageShredder()
    {
        Cost = 3;
        Attack = 3;
        Hp = 3;
        Type = CardType.Object;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        for(int i = 0; i < 3; i++)
        {
            Card selected = RandomSelect(Enemy.garbage);
            if(selected == null)break;
            Enemy.garbage.Remove(selected);
        }
    }
}

public class Antivirus : Card
{
    public Antivirus()
    {
        Cost = 4;
        Attack = 2;
        Hp = 2;
        Type = CardType.Object;
        select = new Select(where.enemyField, 1);
    }

    public override bool ValidateTargets(Player move, Player enemy, List<Card> targets)
    {
        return targets != null && targets.Count == 1 &&
               targets[0].Type == CardType.Object;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Card actualTarget = target != null && target.Count == 1 ? target[0] : null;
        CardImplementationUtilities.DamageObject(player, Enemy, actualTarget, 3);
    }
}

public class Mainframe : Card
{
    public Mainframe()
    {
        Cost = 6;
        Attack = 5;
        Hp = 6;
        Type = CardType.Object;
        isProxy = true;
        isSandBox = true;
        select = new Select();
    }
}

public class DataFetch : Card
{
    public DataFetch()
    {
        Cost = 2;
        Type = CardType.Method;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        player.Draw(2);
    }
}

public class ProcessKill : Card
{
    public ProcessKill()
    {
        Cost = 2;
        Type = CardType.Method;
        select = new Select(where.enemyField, 1);
    }

    public override bool ValidateTargets(Player move, Player enemy, List<Card> targets)
    {
        return targets != null && targets.Count == 1 &&
               targets[0].Type == CardType.Object && targets[0].Hp <= 3;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Card actualTarget = target != null && target.Count > 0 ? target[0] : null;
        if(actualTarget != null)
        {
            player.DestoryField(Enemy, new List<Card>(){actualTarget});
        }
    }
}

public class EmergencyEvasion : Card
{
    public EmergencyEvasion()
    {
        Cost = 5;
        Attack = 2;
        Hp = 6;
        Type = CardType.Object;
        isProxy = true;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        CardImplementationUtilities.DamageAllObjects(player, Enemy, 5);
    }

    public override bool IsFailSafe()
    {
        return player.maxMemory > 0 && player.maxMemory <= 10;
    }

    public override void FailSafe(Player Enemy, List<Card> target = null)
    {
        Card selected = RandomSelect(Enemy.field.FindAll(c=>c.Type == CardType.Object));
        if(selected != null)player.DestoryField(Enemy, new List<Card>(){selected});
        player.Draw(2);
        this.Constructor(Enemy);
    }
}

public class Override : Card
{
    private Card boostedCard;

    public Override()
    {
        Cost = 1;
        Type = CardType.Method;
        select = new Select(where.selfField, 1);
    }

    public override bool ValidateTargets(Player move, Player enemy, List<Card> targets)
    {
        return targets != null && targets.Count == 1 &&
               targets[0].Type == CardType.Object;
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        boostedCard = target != null && target.Count == 1 ? target[0] : null;
        if(boostedCard == null)return;
        boostedCard.Attack += 2;
        boostedCard.ChangeAttack += 2;
        boostedCard.Hp += 2;
        boostedCard.ChangeHp += 2;
    }

    private void RemoveBoost()
    {
        if(boostedCard == null)return;

        if(boostedCard.player != null &&
           boostedCard.player.field.Contains(boostedCard))
        {
            boostedCard.Attack -= 2;
            boostedCard.ChangeAttack -= 2;
            boostedCard.Hp -= 2;
            boostedCard.ChangeHp -= 2;
        }

        boostedCard = null;
    }

    public override void EndPhase(Player Enemy)
    {
        RemoveBoost();
    }

    public override void Destructor(Player Enemy, List<Card> target = null)
    {
        RemoveBoost();
    }
}

public class CacheClear : Card
{
    public CacheClear()
    {
        Cost = 1;
        Type = CardType.Method;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        List<Card> discardedCards = new List<Card>(player.hand);
        foreach(Card c in discardedCards)
        {
            player.hand.Remove(c);
            player.garbage.Add(c);
        }

        player.Draw(discardedCards.Count + 1);
    }
}

public class Format : Card
{
    public Format()
    {
        Cost = 6;
        Type = CardType.Method;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        List<Card> allObjects = new List<Card>();
        allObjects.AddRange(player.field.FindAll(c=>c.Type == CardType.Object));
        allObjects.AddRange(Enemy.field.FindAll(c=>c.Type == CardType.Object));
        player.DestoryField(Enemy, allObjects);
    }
}

public class ApplyPatch : Card
{
    public ApplyPatch()
    {
        Cost = 2;
        Type = CardType.Method;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        Card scope = player.gm.currentScope;
        if(scope != null)
        {
            Player scopeOwner = scope.player;
            Player scopeEnemy = scopeOwner == player ? Enemy : player;

            scope.ExecuteDestructor(scopeEnemy);
            if(scopeOwner != null && !scopeOwner.garbage.Contains(scope))
            {
                scopeOwner.garbage.Add(scope);
            }
            scope.Cost -= scope.ChangeCost;
            scope.ChangeCost = 0;
            player.gm.currentScope = null;
        }
        player.Draw();
    }
}

public class EmergencyPower : Card
{
    public EmergencyPower()
    {
        Cost = 2;
        Type = CardType.Method;
        select = new Select();
    }

    public override void Constructor(Player Enemy, List<Card> target = null)
    {
        player.usedMemory -= 4;
        if(player.usedMemory < 0)player.usedMemory = 0;
        player.Draw();
    }
}
