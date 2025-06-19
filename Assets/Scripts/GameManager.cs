using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("场景设置")]
    [Tooltip("场景中的玩家对象")]
    public GameObject playerObject;
    
    [Header("组件引用")]
    public RhythmManager rhythmManager;
    
    [Header("游戏设置")]
    public bool autoStart = true;
    [Tooltip("网格单元的世界坐标大小，需要匹配tilemap的tile大小")]
    public float gridSize = 1f;
    
    // 游戏对象引用
    private RhythmPlayerController playerController;
    
    void Start()
    {
        InitializeGame();
        
        if (autoStart)
        {
            StartGame();
        }
    }
    
    void Update()
    {
        // 重启游戏
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }
        
        // 暂停/恢复音乐
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }
    
    private void InitializeGame()
    {
        // 获取或创建节拍管理器
        if (rhythmManager == null)
        {
            rhythmManager = FindObjectOfType<RhythmManager>();
            if (rhythmManager == null)
            {
                GameObject rhythmObj = new GameObject("RhythmManager");
                rhythmManager = rhythmObj.AddComponent<RhythmManager>();
                rhythmObj.AddComponent<AudioSource>();
            }
        }
        
        // 设置玩家
        SetupPlayer();
        
        Debug.Log("游戏初始化完成");
    }
    
    private void SetupPlayer()
    {
        if (playerObject == null)
        {
            Debug.LogError("GameManager: 请在Inspector中指定Player Object!");
            return;
        }
        
        Debug.Log($"设置玩家: {playerObject.name}");
        
        // 获取玩家控制器组件（需要手动添加到prefab上）
        playerController = playerObject.GetComponent<RhythmPlayerController>();
        if (playerController == null)
        {
            Debug.LogError("GameManager: Player Object上找不到RhythmPlayerController组件！请手动添加。");
            return;
        }
        
        // 设置网格大小
        playerController.gridSize = gridSize;
        
        // 确保RhythmManager引用正确设置
        if (playerController.rhythmManager == null)
        {
            playerController.rhythmManager = rhythmManager;
        }
        
        Debug.Log("玩家组件设置完成");
    }
    
    public void StartGame()
    {
        if (rhythmManager != null)
        {
            rhythmManager.StartMusic();
        }
        
        Debug.Log("游戏开始！");
    }
    
    public void RestartGame()
    {
        // 重置玩家位置和统计
        if (playerController != null)
        {
            // 重置到初始世界位置，假设是(0.5, 0)
            playerController.SetPosition(new Vector3(0.5f, 0f, 0f));
            playerController.ResetStats();
        }
        
        // 重新开始音乐
        if (rhythmManager != null)
        {
            rhythmManager.StopMusic();
            rhythmManager.StartMusic();
        }
        
        Debug.Log("游戏重启！");
    }
    
    public void TogglePause()
    {
        if (rhythmManager != null)
        {
            if (rhythmManager.IsPlaying)
            {
                rhythmManager.StopMusic();
                Debug.Log("游戏暂停");
            }
            else
            {
                rhythmManager.StartMusic();
                Debug.Log("游戏恢复");
            }
        }
    }
    
    void OnGUI()
    {
        // 放大字体
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 24;
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 24;
        
        // 显示游戏控制说明 (放大)
        GUI.Box(new Rect(Screen.width - 400, Screen.height - 200, 360, 160), "游戏控制", boxStyle);
        GUI.Label(new Rect(Screen.width - 380, Screen.height - 150, 340, 40), "R: 重启游戏", labelStyle);
        GUI.Label(new Rect(Screen.width - 380, Screen.height - 110, 340, 40), "P: 暂停/恢复", labelStyle);
        
        // 显示游戏状态
        string gameState = rhythmManager != null && rhythmManager.IsPlaying ? "播放中" : "已暂停";
        GUI.Label(new Rect(Screen.width - 380, Screen.height - 70, 340, 40), $"状态: {gameState}", labelStyle);
    }
} 