using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class LocalConnect:MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button closeButton;

    [Header("Scene Settings")]
    [SerializeField] private string battleSceneName = "LocalGame";

    private const int BroadcastPort = 50000;
    private const string SecretWord = "ALLOSEIZE_HOST"; 
    private bool isBroadcasting = false;
    private bool isSearching = false;

    private UdpClient broadcaster;
    private UdpClient listener;
    private void Start()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
    public void Close()
    {
        isBroadcasting = false;
        isSearching = false;

        if (broadcaster != null) { broadcaster.Close(); broadcaster = null; }
        if (listener != null) { listener.Close(); listener = null; }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.Shutdown();
        }
        SetUIState(true, "キャンセルしました。対戦モードを選択してください。");
    }
    private void OnDestroy()
    {
        isBroadcasting = false;
        isSearching = false;

        // 電波の道具だけはメモリリーク防止のために確実に壊す
        if (broadcaster != null) { broadcaster.Close(); broadcaster = null; }
        if (listener != null) { listener.Close(); listener = null; }

        // 【超重要】ここで Shutdown() は絶対に呼ばない！！！（通信を維持したまま次のシーンへ行くため）
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
    private string GetLocalIPAddress()
    {
        try
        {
            // 外部のIP（例: GoogleのパブリックDNS）へ向けて通信の準備をする
            // ※実際にはパケットは送信されないので、インターネットに繋がっていなくても動きます
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("IPアドレスの自動取得に失敗しました: " + e.Message);
            return "127.0.0.1"; // 失敗した時の最後の保険
        }
    }
    public void StartHost()
    {
        SetUIState(false,"ホストとしてサーバーを起動しました。対戦相手の接続を待っています...");
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null) 
        {
            string myIp = GetLocalIPAddress();
            transport.ConnectionData.ServerListenAddress = myIp;
            Debug.Log($"ホストのドアを {myIp} に限定して開きました！");
        }
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.StartHost();
        StartBroadcasting();
    }
    private async void StartBroadcasting()
    {
        isBroadcasting = true;
        using (broadcaster = new UdpClient())
        {
            broadcaster.EnableBroadcast = true;
            byte[] data = Encoding.UTF8.GetBytes(SecretWord);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);

            Debug.Log("募集の叫びを開始しました。");

            while (isBroadcasting)
            {
                try
                {
                    await broadcaster.SendAsync(data, data.Length, endPoint);
                    await Task.Delay(1000);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("叫び中にエラー: " + e.Message);
                }
            }
        }
    }
    public async void SearchHost()
    {
        SetUIState(false, "LAN内のホストを探しています...");
        isSearching = true;

        using (listener = new UdpClient(BroadcastPort))
        {
            try
            {
                while (isSearching)
                {
                    UdpReceiveResult result = await listener.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);

                    if (message == SecretWord)
                    {
                        isSearching = false;
                        string hostIP = result.RemoteEndPoint.Address.ToString();
                        
                        statusText.text = $"{hostIP} のホストを発見しました！接続します...";
                        Debug.Log($"ホスト発見！ IP: {hostIP}");

                        JoinHost(hostIP);
                        break; 
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.Log("検索を終了しました: " + e.Message);
            }
        }
    }
    private void JoinHost(string ipAddress)
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = ipAddress;
        NetworkManager.Singleton.StartClient();
    }
    private async void OnClientConnected(ulong clientId)
    {
        Debug.Log($"【通信ログ】誰かが接続しました。現在の接続数: {NetworkManager.Singleton.ConnectedClients.Count}");

        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("【通信ログ】自分はホスト（サーバー）です。人数チェックに入ります。");

            if(NetworkManager.Singleton.ConnectedClients.Count >= 2)
            {
                Debug.Log("【通信ログ】2人揃いました！シーン遷移を指示します！");
                
                if(statusText != null)
                {
                    statusText.text = "対戦相手が接続しました。バトルシーンへ遷移します...";
                }
                isBroadcasting = false;
                await Task.Delay(1000);
                NetworkManager.Singleton.SceneManager.LoadScene(battleSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    private void SetUIState(bool clickable, string message)
    {
        if (hostButton != null) hostButton.interactable = clickable;
        if (clientButton != null) clientButton.interactable = clickable;
        if (statusText != null) statusText.text = message;
    }
}
