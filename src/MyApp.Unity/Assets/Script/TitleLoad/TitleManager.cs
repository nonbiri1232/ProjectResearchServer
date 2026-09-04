using System.Security.Cryptography.X509Certificates;
using TMPro;
using UnityEngine;

public class TitleManager : MonoBehaviour
{
    public GameObject alert;
    public TextMeshProUGUI alertText;
    public SceneTransition sceneTransition;
    [Header("ローカル通信用")]
    public GameObject localSelect;
    public GameObject Host;
    public GameObject Client;
    public GameObject Status;
    public GameObject Close;
    public GameObject Cancel;
    public void GoToOffline()
    {
        if(DeckManager.player1Deck.Count == DeckManager.MAXDECKNUM && DeckManager.player2Deck.Count == DeckManager.MAXDECKNUM)
        {
            sceneTransition.GoToLoadingScene("OfflineGame");
        }
        else
        {
            alert.SetActive(true);
            if (DeckManager.player1Deck.Count == DeckManager.MAXDECKNUM && DeckManager.player2Deck.Count != DeckManager.MAXDECKNUM)
            {
                alertText.text = "Player2のデッキの枚数が足りません。";
            }
            else if (DeckManager.player1Deck.Count != DeckManager.MAXDECKNUM && DeckManager.player2Deck.Count == DeckManager.MAXDECKNUM)
            {
                alertText.text = "Player1のデッキの枚数が足りません。";
            }
            else
            {
                alertText.text = "Player1とPlayer2のデッキの枚数が足りません。";
            }
        }
    }
    public void GoToAIBattle()
    {
        if(DeckManager.player1Deck.Count == DeckManager.MAXDECKNUM)
        {
            sceneTransition.GoToLoadingScene("AIBattle");
        }
        else
        {
            alert.SetActive(true);
            alertText.text = "Player1のデッキの枚数が足りません。";
        }
    }
    public void OpenSelectLocal()
    {
        if(DeckManager.player1Deck.Count == DeckManager.MAXDECKNUM)
        {
            localSelect.SetActive(true);
            Host.SetActive(true);
            Client.SetActive(true);
            Status.SetActive(false);
            Close.SetActive(true);
            Cancel.SetActive(false);
        }
        else
        {
            alert.SetActive(true);
            alertText.text = "Player1のデッキの枚数が足りません。";
        }
    }
    public void ShutDown()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else   
            Application.Quit();
        #endif
    }
}
