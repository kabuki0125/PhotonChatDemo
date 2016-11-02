using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// View：チャンネルボタンToggle.
/// </summary>
public class View_ChannelToggle : ViewBase
{
    /// <summary>トグル名.</summary>
    public string ChannelName { get; private set; }
    
    /// <summary>トグルのオンオフ.</summary>
    public bool IsOn 
    {
        get {
            return this.GetComponent<Toggle>().isOn;
        }
        set {
            this.GetComponent<Toggle>().isOn = value;
        }
    }
    
    /// <summary>トグルの値変化時のイベント.</summary>
    public event Action<string/*channelName*/> DidToggleActive;
    
    /// <summary>
    /// 初期化.
    /// </summary>
	public void Init(string name, bool active)
    {
        this.ChannelName = name;
        this.GetScript<Text>("Label").text = this.ChannelName;
        this.IsOn = active;
        
        this.GetComponent<Toggle>().onValueChanged.AddListener(OnValueChanged);
    }
    
    // コールバック：トグルの値変化
    void OnValueChanged(bool bActive)
    {
        if(!bActive){
            return;
        }
        if(this.DidToggleActive != null){
            this.DidToggleActive(this.ChannelName);
        }
    }
}
