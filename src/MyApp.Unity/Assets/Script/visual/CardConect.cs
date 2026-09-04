using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class CardSetting
{
    public string className;
    public int cost;
    public int atk;
    public int hp;
    public string displayName;
    public string ability;
    public Sprite cardImage;

}
[CreateAssetMenu(fileName = "CardConect", menuName = "Scriptable Objects/CardConect")]
public class CardConect : ScriptableObject
{
    public List<CardSetting> cards = new List<CardSetting>();
}
