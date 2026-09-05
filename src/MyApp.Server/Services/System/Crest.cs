using System.Collections.Generic;
using System.Linq;

public class Crest
{
    public Player turn,noTurn;
    public List<Card> effectOnPlay = new List<Card>();
    public List<Card> effectOnAttack = new List<Card>();

    public Crest(Player first,Player second)
    {
        turn = first;
        noTurn = second;        
    }

    public bool IsCrestOnAttack()
    {
        if(effectOnAttack.Count > 0)
        {
            return true;
        }
        return false;
    }
    public bool IsCrestOnPlay()
    {
        if(effectOnPlay.Count > 0)
        {
            return true;
        }
        return false;
    }

    public void OnAttack(Card source,Card target)
    {
        var list = new List<Card>(){source,target};
        foreach(var c in effectOnAttack.ToList())
        {
            if(c.player != turn)continue;
            c.CrestOnAttack(noTurn,list);
        }
    }

    public void OnPlay(Card play)
    {
        foreach(var c in effectOnPlay.ToList())
        {
            if(c.player != turn)continue;
            c.CrestOnPlay(noTurn,play);
        }
    }
}