using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// View：チャットウィンドウ.
/// </summary>
public class View_ChatWindow : ViewBase
{
    // とりあえずスタート起動.出し入れするウィンドウか在中するかどうかでこの辺りは変える.
	void Start()
    {
        m_listener = GameObjectEx.LoadAndCreateObject("ChatManager").GetComponent<ChatListener>();
        m_listener.DidGetGlobalMessage += DidGetGlobalMessageProc;
        
        m_errorPanel = this.GetScript<Image>("AppId Panel");
        m_pickNamePanel = this.GetScript<Image>("PickName Panel");
        m_chatPanel = this.GetScript<Image>("Chat Panel");
        m_logText = this.GetScript<Text>("Selected Channel Text");
        
        m_inputChatField = this.GetScript<InputField>("Chat InputField");
        m_userNameField = this.GetScript<InputField>("InputField");
        
        // ボタン設定.
        this.GetScript<Button>("Panel/Button").onClick.AddListener(DidTapNameDecide);
        this.GetScript<Button>("InputBar Panel/Chat Send Button").onClick.AddListener(DidTapSend);
        
        this.SetActivePanel(PanelType.PickName);
    }
    
    // チャットウィンドウの切り替え.
    private void SetActivePanel(PanelType type)
    {
        m_errorPanel.gameObject.SetActive(type == PanelType.Error);
        m_pickNamePanel.gameObject.SetActive(type == PanelType.PickName);
        m_chatPanel.gameObject.SetActive(type == PanelType.Chat);
    }
    
    // チャンネル切り替え時.
    private void ChannelChanged(string channelName)
    {
        foreach(var tgl in m_toggleList){
            // トグルの切り替え.
            tgl.IsOn = tgl.ChannelName == channelName;
            // 文言表示の切り替え.
            if(tgl.IsOn){
                m_currentChannelName = tgl.ChannelName;
                m_listener.ChangeChannel(m_currentChannelName);
            }
        }
    }
    
#region ButtonEvents.
    
    // 名前決定ボタン押下.
    void DidTapNameDecide()
    {
        if(string.IsNullOrEmpty(m_userNameField.text)){
            return;
        }
        
        this.SetActivePanel(PanelType.None);
        m_listener.Init(m_userNameField.text, 
                        () => { this.SetActivePanel(PanelType.Chat); View_ChannelToggle.DidToggleActive += ChannelChanged; },
                        DidSubScribe);
        
        m_userNameField.text = "";
    }
    
    // チャット送信ボタン.
    void DidTapSend()
    {
        if(string.IsNullOrEmpty(m_inputChatField.text)){
            return;
        }
        
        Debug.Log("[View_ChatWindow] DidTapSend : message="+m_inputChatField.text);
        m_listener.SendChatMessage(m_inputChatField.text);
        m_inputChatField.text = "";
        
        // 特定のチャンネル(システム)には発言できない.“システム”で発言しようとしていた場合はチャンネルを”全体"に移動.
        if(m_currentChannelName == "システム"){
            this.ChannelChanged("全体");
        }
    }
    
#endregion
    
    // コールバック：購読開始
    void DidSubScribe(string currentName, string[] channels, bool[] results)
    {
        // 返ってきたチャンネル数分タブを作る.
        for(var i = 0 ; i < channels.Length ; i++){
            if(!results[i]){
                continue;
            }
            if( m_toggleList.Exists(tgl => tgl.ChannelName == channels[i]) ){
                continue;   // 再接続時を考慮.
            }
            var go = GameObjectEx.LoadAndCreateObject("Channel Toggle");
            go.GetComponent<RectTransform>().SetParent(this.GetScript<Image>("ChannelBar Panel").transform);    // RectTransformの親設定は通常と異なる.
            var com = go.GetOrAddComponent<View_ChannelToggle>();
            com.Init(channels[i], channels[i] == currentName);
            m_toggleList.Add(com);
        }
    }
    
    // コールバック：グローバルチャットメッセージ取得.
    void DidGetGlobalMessageProc(string channelName, string newMessage)
    {
        // メッセージの更新.
        m_logText.text = "";
        m_logText.text = newMessage;
    }
    
    
#region debug
    
    void OnGUI()
    {
        if(!m_listener.IsInit){
            return;
        }
        
        var text = m_bDebugSending ? "Stop auto message." : "Send message loop with auto.";
        if(GUILayout.Button(text)){
            if(m_bDebugSending){
                this.StopCoroutine("SendLoopMessage");
            }else{
                this.StartCoroutine("SendLoopMessage");
            }
            m_bDebugSending = !m_bDebugSending;
        }
    }
    private IEnumerator SendLoopMessage()
    {
        while(true){
            m_listener.SendChatMessage("test");
            yield return new WaitForSeconds(0.1f);
        }
    }
    private bool m_bDebugSending = false;
    
#endregion
    
    
    private ChatListener m_listener;    // TODO : 実際に使用する場合はListenerはアプリ起動直後に初期化、常駐させるのがベター.
    
    private Image m_errorPanel;
    private Image m_pickNamePanel;
    private Image m_chatPanel;
    private Text m_logText;
    
    private InputField m_inputChatField;
    private InputField m_userNameField;
    
    private string m_currentChannelName;
    
    private List<View_ChannelToggle> m_toggleList = new List<View_ChannelToggle>();
    
    
    // enum : パネルタイプ.
    private enum PanelType
    {
        None, 
        Error, 
        PickName,
        Chat,
    }
}
