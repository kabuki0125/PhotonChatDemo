using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon.Chat;

/// <summary>
/// クラス:チャット操作用モジュール.
/// </summary>
public class ChatListener : MonoBehaviour, IChatClientListener
{
    /// <summary>チャットチャンネル.設定したい種類分追加しておく.</summary>
    [SerializeField]
    private string[] channels;
    
    /// <summary>初回に取ってくる過去ログの長さ.</summary>
    [SerializeField]
    private int logLengthToFetch = 10; 
    
    /// <summary>ログのキャッシュキャパ.</summary>
    [SerializeField]
    private int logCacheCapacity = 100;
    
    [Header("Connection Settings")]
    /// <summary>再接続リトライ上限.</summary>
    [SerializeField]
    private int retryLimitCnt = 3;
    
    /// <summary>再接続時のリトライ間隔.</summary>
    [SerializeField]
    private float retryIntervalSec = 5f;
    
    
    /// <summary>グローバルチャットメッセージ取得.</summary>
    public event Action<string/*channelName*/,string/*newMessage*/> DidGetGlobalMessage;
    /// <summary>プライベートチャットメッセージ取得.</summary>
    public event Action<string/*sender*/,object/*message*/,string/*channelName*/> DidGetPrivateMessge;
    
    /// <summary>初期化が終わってる？</summary>
    public bool IsInit { get; private set; }
    
    
    /// <summary>
    /// 初期化.
    /// </summary>
	public void Init(string userName, Action didConnect = null, Action<string,string[],bool[]> didSubscribe = null)
    {
        if(string.IsNullOrEmpty(userName)){
            Debug.LogError("[ChatManager] Init Error!! : user name is null or empty.");
            return;
        }
        
        m_valLogFetch = logLengthToFetch;
        m_userName = userName;
        m_didConnect = didConnect;
        m_didSubscrib = didSubscribe;
        this.StartCoroutine("StartConnect");
    }
    // 接続開始.
    private IEnumerator StartConnect()
    {
        m_client = new ChatClient(this);
        m_client.MessageLimit = logCacheCapacity;
        var bConnect = m_client.Connect( STR_APP_ID, "1.0", new ExitGames.Client.Photon.Chat.AuthenticationValues(m_userName) );
        
        // 失敗.
        if(!bConnect){
            if(m_cntRetry < retryLimitCnt){
                yield return new WaitForSeconds(retryIntervalSec);
                this.StartCoroutine("StartConnect");
                m_cntRetry++;
                Debug.LogWarning("[ChatManager] StartConnect : failed to connect. retry="+m_cntRetry);
                yield break;
            }else{
                Debug.LogError("[ChatManager] StartConnect Error!! : failed to connect server.");
                yield break;
            }
        }
        
        // 成功.
        this.StartCoroutine("UpdateProcess");
    }
    // 処理更新.
    IEnumerator UpdateProcess()
    {
        while(m_client != null){
            m_client.Service();
            yield return null;
        }
    }
    
    /// <summary>
    /// メッセージ送信.
    /// </summary>
    public void SendChatMessage(string message)
    {
        if(!this.IsInit){
            return;
        }
        if(string.IsNullOrEmpty(message)){
            Debug.LogWarning("[ChatManager] SendMessage Warning : message is null or empty");
            return;
        }
        
        // 特定のチャンネル(システム)には発言できない.
        var channel = m_currentChannel;
        if(m_currentChannel == "システム"){
            channel = "全体";
        }
        m_client.PublishMessage(channel, message);
    }
    
    /// <summary>
    /// チャンネル変更.
    /// </summary>
    public void ChangeChannel(string channelName)
    {
        if(channels == null || channels.Length <= 0){
            return;
        }
        
        m_currentChannel = channelName;
        var message = this.GetChannelLog(m_currentChannel);
        
        // TODO : グローバルメッセージとして.プライベートチャットかどうかをここで判断する必要が有る.
        if(this.DidGetGlobalMessage != null){
            this.DidGetGlobalMessage( m_currentChannel, message );
        }
    }
    
    // 現在チャンネルのログを取得.
    private string GetChannelLog(string channelName)
    {
        var message = "";
        
        // 全体には全てのチャンネルのメッセージを表示.
        switch(m_currentChannel){
        case "全体":
            foreach(var str in m_logList){
                message += str;
            }
            /*
            // TODO : こっちの方法でやるとチャンネル毎の表示になってしまい順番が時系列順にならない.戒めにとっておく.
            foreach(var kvp in m_client.PublicChannels){
                message += kvp.Value.ToStringMessages();
            }
            foreach(var kvp in m_client.PrivateChannels){
                message += kvp.Value.ToStringMessages();
            }
            */
            break;
        default:
            {
                ChatChannel channel;
                if( !m_client.TryGetChannel(channelName, out channel) ){
                    // とりあえず見知らぬチャンネルのメッセージは無視.
                    break;
                }
                message = channel.ToStringMessages();
                message = this.ColoringText(message, channelName);
            }
            break;
        }
        
        return message;
    }

#region implements interface members
    
    /// <summary>ライブラリの全てのデバッグ出力はこのメソッドを経由して流れてくる.</summary>
    public void DebugReturn(ExitGames.Client.Photon.DebugLevel level, string message)
    {
        switch(level){
        case ExitGames.Client.Photon.DebugLevel.ERROR:
            Debug.LogError(message);
            break;
        case ExitGames.Client.Photon.DebugLevel.WARNING:
            Debug.LogWarning(message);
            break;
        default:
            Debug.Log(message);
            break;
        }
    }
    
    /// <summary>切断した際のコールバック.</summary>
    public void OnDisconnected()
    {
        Debug.Log("[ChatManager] OnDisconnected : disconnect server.");
        
        // リトライ.
        Debug.Log("retry connect. cnt="+m_cntRetry);
        this.StartCoroutine("StartConnect");
    }

    /// <summary>接続時のコールバック.</summary>
    public void OnConnected()
    {
        m_client.AddFriends(FRIEND_LIST_DEMO);             // フレンドをステータスに追加.
        m_client.SetOnlineStatus(ChatUserStatus.Online);   // フレンドが追加されていればオンライン状態を表示できる.
        
        // メッセージの購読.指定数分の過去ログを取得することも可能.
        if(channels != null && channels.Length > 0){
            m_currentChannel = channels[0];
            m_client.Subscribe(channels, m_valLogFetch);
            m_valLogFetch = 0;
        }
        
        this.IsInit = true; // ここで初期化終了.
        
        if(m_didConnect != null){
            m_didConnect();
            m_didConnect = null;
        }
        
        m_client.PublishMessage("システム", m_userName+"さんが入室しました！");
    }

    /// <summary>接続状態に変更があった場合通知される.現状はOnConnectedとOnDisconnectedを使用するが将来的により多くのステータスが追加された場合使用予定とのこと.</summary>
    public void OnChatStateChange(ChatState state){}

    /// <summary>新規メッセージ取得時の処理.同タイミングで渡されてきたメッセージはまとまって返ってくる.senderの長さ=messagesの長さで、各要素番号のsenderがそれぞれの要素番号のmessagesを送信している.</summary>
    public void OnGetMessages(string channelName, string[] senders, object[] messages)
    {
        if(senders == null || senders.Length <= 0){
            return;
        }
        if(messages == null || messages.Length <= 0){
            return;
        }
        
        // TODO : ”全体”チャンネル用にログをキャッシュ.処理をまとめるべき.
        for(var i = 0 ; i < messages.Length ; i++){
            if(m_logList.Count >= logCacheCapacity){
                m_logList.RemoveAt(0);
            }
            var suffix = i <= messages.Length-1 ? "\n" : "";
            var msg = senders[i]+": "+(messages[i] as string)+suffix;   // user名: メッセージの形式にして、
            msg = this.ColoringText(msg, channelName);              // 該当のチャンネル毎に色を設定して、
            m_logList.Add( msg );   // 追加する。
        }
        
        var message = this.GetChannelLog(channelName);
        // TODO : グローバルメッセージとして.プライベートチャットかどうかをここで判断する必要が有る.
        if(this.DidGetGlobalMessage != null){
            this.DidGetGlobalMessage( m_currentChannel, message );
        }
    }

    /// <summary>プライベートメッセージの取得.</summary>
    public void OnPrivateMessage(string sender, object message, string channelName)
    {
        if(sender == null || message == null){
            return;
        }
        
        if(this.DidGetPrivateMessge != null){
            this.DidGetPrivateMessge(sender, message, channelName);
        }
    }

    /// <summary>購読操作の結果.要求したチャンネル分が返ってくる.</summary>
    public void OnSubscribed(string[] channels, bool[] results)
    {
        if(m_didSubscrib != null){
            m_didSubscrib(m_currentChannel, channels, results);
        }
    }

    /// <summary>登録解除操作の結果.チャンネルを退会している場合チャンネル名を返す.</summary>
    public void OnUnsubscribed(string[] channels)
    {
    }

    public void OnStatusUpdate(string user, int status, bool gotMessage, object message)
    {
    }
    
#endregion
    
    // 指定テキストを指定されたチャンネル毎に着色する.
    private string ColoringText(string text, string channelName)
    {
        var baseText = "<color={0}>"+text+"</color>";
        switch(channelName){
        case "全体":
            return string.Format(baseText, "white");
        case "パーティ":
            return string.Format(baseText, "magenta");
        case "ギルド":
            return string.Format(baseText, "purple");
        case "システム":
            return string.Format(baseText, "yellow");
        }
        return null;
    }
    
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    void OnEnable()
    {
        // アプリサスペンド時はリトライ.
        if(!this.IsInit){
            return;
        }
        this.StartCoroutine("StartConnect");
    }
    
    private int m_valLogFetch;  // 取ってくるログの長さ.初回起動時以降、再接続時はログ表示がおかしくなるので0になる.
    private Action m_didConnect;        // 接続成功時の処理.
    private Action<string,string[],bool[]> m_didSubscrib;  // 購読開始時の処理.
    private ChatClient m_client;
    private string m_userName;
    private int m_cntRetry;
    private string m_currentChannel;    // 現在選択中のチャンネル名.
    
    private List<string> m_logList = new List<string>();     // チャンネルに依存しない時系列順の過去ログ.
    
    private static readonly string STR_APP_ID = "5bd08206-1fdd-4405-bbd3-74871ee85b53";     // TODO : 実際はサーバーから落としてくるなりクライアントでDefineをきるなりする.
    private static readonly string[] FRIEND_LIST_DEMO = new string[]{ "かえで", "まさる" };   // TODO : 実際はサーバーから予めフレンドリストを取得しておく必要あり.
}
