using System;
using UnityEngine;
using UnityEngine.Events;

public class RhythmManager : MonoBehaviour
{
    [Header("节拍设置")]
    public float beatsPerMinute = 115f;     // 修改为Disco Descent的BPM
    [Range(0.05f, 0.5f)]
    public float goodWindow = 0.15f;     // 良好时机窗口
    
    [Header("音频设置")]
    public AudioSource audioSource;
    public bool useManualBPM = true;     // 是否使用手动BPM（而不是从音频分析）
    
    [Header("调试信息")]
    public bool showDebugInfo = true;
    
    // 节拍事件
    [System.Serializable]
    public class BeatEvent : UnityEvent { }
    [System.Serializable]
    public class TimingEvent : UnityEvent<TimingType> { }
    
    public BeatEvent OnBeat = new BeatEvent();
    public TimingEvent OnTimingEvaluated = new TimingEvent();
    
    // 时机判定类型
    public enum TimingType { Miss, Good }
    
    // 私有变量
    private float beatInterval;
    private float nextBeatTime;
    private float songPosition;
    private float songPositionInBeats;
    private float dspSongTime;
    private int currentBeat;
    private bool isPlaying = false;
    
    // 公共属性
    public float CurrentBeatProgress => (songPositionInBeats % 1f);
    public int CurrentBeat => currentBeat;
    public bool IsPlaying => isPlaying && audioSource.isPlaying;
    public float BeatInterval => beatInterval;  // 节拍间隔（秒）
    public float CurrentSongPosition => songPosition;  // 当前歌曲位置（秒）
    public float bpm => beatsPerMinute;  // BPM属性
    
    void Start()
    {
        // 计算节拍间隔
        beatInterval = 60f / beatsPerMinute;
        
        // 如果没有指定AudioSource，尝试获取
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
            
        if (audioSource == null)
        {
            Debug.LogError("RhythmManager: 没有找到AudioSource组件！");
            return;
        }
        
        // 开始播放
        StartMusic();
    }
    
    void Update()
    {
        if (!IsPlaying) return;
        
        // 计算当前歌曲位置
        songPosition = (float)(AudioSettings.dspTime - dspSongTime);
        songPositionInBeats = songPosition / beatInterval;
        
        // 检测是否到达新的节拍
        int newBeat = Mathf.FloorToInt(songPositionInBeats);
        if (newBeat > currentBeat)
        {
            currentBeat = newBeat;
            OnBeatHit();
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        // 放大字体
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 24;
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 24;
        
        GUI.Box(new Rect(20, 20, 600, 300), "节拍调试信息", boxStyle);
        GUI.Label(new Rect(40, 70, 560, 40), $"BPM: {beatsPerMinute}", labelStyle);
        GUI.Label(new Rect(40, 110, 560, 40), $"当前节拍: {currentBeat}", labelStyle);
        GUI.Label(new Rect(40, 150, 560, 40), $"节拍进度: {CurrentBeatProgress:F2}", labelStyle);
        GUI.Label(new Rect(40, 190, 560, 40), $"歌曲位置: {songPosition:F2}s", labelStyle);
        GUI.Label(new Rect(40, 230, 560, 40), $"是否播放: {IsPlaying}", labelStyle);
        
        // 节拍可视化条 (放大)
        Rect beatBar = new Rect(40, 270, 560, 40);
        GUI.Box(beatBar, "");
        
        // 绘制节拍进度 (使用DrawTexture绘制白色进度条)
        float progress = CurrentBeatProgress;
        Rect progressRect = new Rect(beatBar.x, beatBar.y, beatBar.width * progress, beatBar.height);
        GUI.DrawTexture(progressRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Color.white, 0, 0);
        
        // 绘制良好时机窗口 (使用DrawTexture绘制绿色窗口)
        float goodStart = (1f - goodWindow) * beatBar.width;
        Rect goodRect = new Rect(beatBar.x + goodStart, beatBar.y, goodWindow * beatBar.width, beatBar.height);
        GUI.DrawTexture(goodRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Color.green, 0, 0);
    }
    
    public void StartMusic()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            dspSongTime = (float)AudioSettings.dspTime;
            audioSource.Play();
            isPlaying = true;
            currentBeat = 0;
            Debug.Log("音乐开始播放");
        }
        else
        {
            Debug.LogWarning("无法开始播放：AudioSource或AudioClip为空");
        }
    }
    
    public void StopMusic()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            isPlaying = false;
            Debug.Log("音乐停止播放");
        }
    }
    
    private void OnBeatHit()
    {
        OnBeat.Invoke();
        if (showDebugInfo)
        {
            Debug.Log($"节拍 {currentBeat} | 进度: {CurrentBeatProgress:F3}");
        }
    }
    
    /// <summary>
    /// 获取当前时刻相对于节拍的准确度 (-0.5 到 0.5，0为完美时机)
    /// </summary>
    public float GetBeatAccuracy()
    {
        float progress = CurrentBeatProgress;
        // 将进度转换为相对于节拍点的偏差
        float accuracy;
        if (progress > 0.5f)
            accuracy = progress - 1f; // 早于节拍
        else
            accuracy = progress; // 晚于节拍
            
        //Debug.Log($"[RhythmManager] GetBeatAccuracy: progress={progress:F3}, accuracy={accuracy:F3}");
        return accuracy;
    }
    
    /// <summary>
    /// 评估输入时机
    /// </summary>
    public TimingType EvaluateTiming(float accuracy)
    {
        float absAccuracy = Mathf.Abs(accuracy);
        
        TimingType timing;
        if (absAccuracy <= goodWindow)
            timing = TimingType.Good;
        else
            timing = TimingType.Miss;
            
        Debug.Log($"[RhythmManager] EvaluateTiming: accuracy={accuracy:F3}, absAccuracy={absAccuracy:F3}, goodWindow={goodWindow:F3}, timing={timing}");
            
        OnTimingEvaluated.Invoke(timing);
        return timing;
    }
    
    /// <summary>
    /// 检查当前是否在可行动的时间窗口内
    /// </summary>
    public bool IsInActionWindow()
    {
        float accuracy = GetBeatAccuracy();  
        float absAccuracy = Mathf.Abs(accuracy);
        bool inWindow = absAccuracy <= goodWindow;
        
        Debug.Log($"[RhythmManager] IsInActionWindow: accuracy={accuracy:F3}, absAccuracy={absAccuracy:F3}, goodWindow={goodWindow:F3}, inWindow={inWindow}");
        
        return inWindow;
    }
} 