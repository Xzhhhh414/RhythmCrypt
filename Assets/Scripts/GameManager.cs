using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("场景设置")]
    public GameObject playerPrefab;
    public Transform playerSpawnPoint;
    
    [Header("组件引用")]
    public RhythmManager rhythmManager;
    public Camera gameCamera;
    
    [Header("游戏设置")]
    public bool autoStart = true;
    public float gridSize = 1f;
    
    // 游戏对象引用
    private GameObject playerObject;
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
        
        // 设置摄像机
        if (gameCamera == null)
            gameCamera = Camera.main;
            
        // 创建玩家
        CreatePlayer();
        
        Debug.Log("游戏初始化完成");
    }
    
    private void CreatePlayer()
    {
        Vector3 spawnPosition = playerSpawnPoint != null ? 
            playerSpawnPoint.position : Vector3.zero;
            
        if (playerPrefab != null)
        {
            // 使用预制件创建玩家
            playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            Debug.Log("使用Player Prefab创建玩家");
        }
        else
        {
            // 创建默认的Cube玩家
            playerObject = CreateDefaultPlayer(spawnPosition);
            Debug.Log("创建默认Cube玩家");
        }
        
        // 确保玩家组件正确设置
        SetupPlayerComponents();
        
        // 设置摄像机跟随
        if (gameCamera != null)
        {
            gameCamera.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, -10f);
        }
    }
    
    private GameObject CreateDefaultPlayer(Vector3 position)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player.name = "Player";
        player.transform.position = position;
        player.transform.localScale = Vector3.one * 0.8f;
        
        // 设置颜色
        Renderer renderer = player.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.cyan;
            renderer.material = mat;
        }
        
        return player;
    }
    
    private void SetupPlayerComponents()
    {
        if (playerObject == null) return;
        
        // 获取或添加网格移动组件
        GridMovement gridMovement = playerObject.GetComponent<GridMovement>();
        if (gridMovement == null)
        {
            gridMovement = playerObject.AddComponent<GridMovement>();
            Debug.Log("自动添加GridMovement组件");
        }
        gridMovement.gridSize = gridSize; // 确保网格大小正确
        
        // 获取或添加节拍玩家控制器
        playerController = playerObject.GetComponent<RhythmPlayerController>();
        if (playerController == null)
        {
            playerController = playerObject.AddComponent<RhythmPlayerController>();
            Debug.Log("自动添加RhythmPlayerController组件");
        }
        
        // 确保引用正确设置
        playerController.rhythmManager = rhythmManager;
        playerController.gridMovement = gridMovement;
        
        Debug.Log($"玩家组件设置完成 - GridMovement: {gridMovement != null}, RhythmPlayerController: {playerController != null}");
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
        // 重置玩家位置
        if (playerObject != null && playerSpawnPoint != null)
        {
            GridMovement gridMovement = playerObject.GetComponent<GridMovement>();
            if (gridMovement != null)
            {
                gridMovement.SetGridPosition(Vector2Int.zero);
            }
        }
        
        // 重置统计
        if (playerController != null)
        {
            playerController.perfectHits = 0;
            playerController.goodHits = 0;
            playerController.missedInputs = 0;
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
        // 显示游戏控制说明
        GUI.Box(new Rect(Screen.width - 200, Screen.height - 120, 180, 100), "游戏控制");
        GUI.Label(new Rect(Screen.width - 190, Screen.height - 95, 170, 20), "R: 重启游戏");
        GUI.Label(new Rect(Screen.width - 190, Screen.height - 75, 170, 20), "P: 暂停/恢复");
        GUI.Label(new Rect(Screen.width - 190, Screen.height - 55, 170, 20), "空格: 切换节拍模式");
        
        // 显示游戏状态
        string gameState = rhythmManager != null && rhythmManager.IsPlaying ? "播放中" : "已暂停";
        GUI.Label(new Rect(Screen.width - 190, Screen.height - 35, 170, 20), $"状态: {gameState}");
    }
} 