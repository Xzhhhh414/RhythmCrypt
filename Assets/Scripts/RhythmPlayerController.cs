using UnityEngine;

public class RhythmPlayerController : MonoBehaviour
{
    [Header("组件引用")]
    public RhythmManager rhythmManager;
    public GridMovement gridMovement;
    
    [Header("控制设置")]
    public bool requireBeatTiming = true;  // 是否要求按节拍输入
    public bool allowEarlyInput = true;    // 是否允许提前输入
    
    [Header("输入缓冲")]
    [Range(0f, 0.5f)]
    public float inputBufferTime = 0.1f;   // 输入缓冲时间
    
    [Header("反馈设置")]
    public bool showTimingFeedback = true;
    
    // 输入状态
    private Vector2Int bufferedInput = Vector2Int.zero;
    private float inputBufferTimer = 0f;
    private bool hasBufferedInput = false;
    
    // 统计信息
    public int perfectHits = 0;
    public int goodHits = 0;
    public int missedInputs = 0;
    
    // 反馈显示
    private string lastTimingFeedback = "";
    private float feedbackDisplayTime = 0f;
    private Color feedbackColor = Color.white;
    
    void Start()
    {
        // 自动获取组件引用
        if (rhythmManager == null)
            rhythmManager = FindObjectOfType<RhythmManager>();
            
        if (gridMovement == null)
            gridMovement = GetComponent<GridMovement>();
            
        // 订阅节拍事件
        if (rhythmManager != null)
        {
            rhythmManager.OnBeat.AddListener(OnBeat);
            rhythmManager.OnTimingEvaluated.AddListener(OnTimingEvaluated);
        }
        
        // 检查必要组件
        if (rhythmManager == null)
            Debug.LogError("RhythmPlayerController: 找不到RhythmManager!");
        if (gridMovement == null)
            Debug.LogError("RhythmPlayerController: 找不到GridMovement组件!");
    }
    
    void Update()
    {
        // 处理输入
        HandleInput();
        
        // 更新输入缓冲
        UpdateInputBuffer();
        
        // 更新反馈显示时间
        if (feedbackDisplayTime > 0)
            feedbackDisplayTime -= Time.deltaTime;
    }
    
    void OnGUI()
    {
        // 显示统计信息
        GUI.Box(new Rect(Screen.width - 200, 10, 180, 100), "玩家统计");
        GUI.Label(new Rect(Screen.width - 190, 35, 170, 20), $"完美: {perfectHits}");
        GUI.Label(new Rect(Screen.width - 190, 55, 170, 20), $"良好: {goodHits}");
        GUI.Label(new Rect(Screen.width - 190, 75, 170, 20), $"错过: {missedInputs}");
        
        // 显示时机反馈
        if (showTimingFeedback && feedbackDisplayTime > 0)
        {
            Vector3 playerScreenPos = Camera.main.WorldToScreenPoint(transform.position);
            playerScreenPos.y = Screen.height - playerScreenPos.y + 30;
            
            GUI.color = feedbackColor;
            GUI.Label(new Rect(playerScreenPos.x - 50, playerScreenPos.y, 100, 30), lastTimingFeedback);
            GUI.color = Color.white;
        }
        
        // 显示控制说明
        GUI.Box(new Rect(10, Screen.height - 120, 200, 100), "控制说明");
        GUI.Label(new Rect(20, Screen.height - 95, 180, 20), "WASD / 方向键: 移动");
        GUI.Label(new Rect(20, Screen.height - 75, 180, 20), "按节拍输入获得加成");
        GUI.Label(new Rect(20, Screen.height - 55, 180, 20), $"需要节拍: {(requireBeatTiming ? "是" : "否")}");
        GUI.Label(new Rect(20, Screen.height - 35, 180, 20), "空格: 切换节拍要求");
    }
    
    private void HandleInput()
    {
        // 获取输入方向
        Vector2Int inputDirection = GetInputDirection();
        
        if (inputDirection != Vector2Int.zero)
        {
            if (requireBeatTiming)
            {
                // 需要按节拍输入
                HandleRhythmInput(inputDirection);
            }
            else
            {
                // 自由移动模式
                AttemptMove(inputDirection, 1f, RhythmManager.TimingType.Good);
            }
        }
        
        // 切换节拍要求
        if (Input.GetKeyDown(KeyCode.Space))
        {
            requireBeatTiming = !requireBeatTiming;
            Debug.Log($"节拍要求: {(requireBeatTiming ? "开启" : "关闭")}");
        }
    }
    
    private Vector2Int GetInputDirection()
    {
        Vector2Int direction = Vector2Int.zero;
        
        // WASD 输入
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            direction = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            direction = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            direction = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            direction = Vector2Int.right;
            
        return direction;
    }
    
    private void HandleRhythmInput(Vector2Int direction)
    {
        if (rhythmManager == null) return;
        
        float beatAccuracy = rhythmManager.GetBeatAccuracy();
        RhythmManager.TimingType timing = rhythmManager.EvaluateTiming(beatAccuracy);
        
        // 检查是否在可行动窗口内
        if (rhythmManager.IsInActionWindow())
        {
            // 立即执行移动
            float timingBonus = GetTimingBonus(timing);
            AttemptMove(direction, timingBonus, timing);
        }
        else if (allowEarlyInput && inputBufferTime > 0)
        {
            // 如果允许提前输入且有缓冲时间，则缓存输入
            BufferInput(direction);
        }
        else
        {
            // 输入时机不对
            missedInputs++;
            ShowTimingFeedback("错过时机!", Color.red);
        }
    }
    
    private void BufferInput(Vector2Int direction)
    {
        bufferedInput = direction;
        inputBufferTimer = inputBufferTime;
        hasBufferedInput = true;
    }
    
    private void UpdateInputBuffer()
    {
        if (!hasBufferedInput) return;
        
        inputBufferTimer -= Time.deltaTime;
        
        // 检查是否到了可以执行缓冲输入的时机
        if (rhythmManager != null && rhythmManager.IsInActionWindow())
        {
            float beatAccuracy = rhythmManager.GetBeatAccuracy();
            RhythmManager.TimingType timing = rhythmManager.EvaluateTiming(beatAccuracy);
            float timingBonus = GetTimingBonus(timing);
            
            AttemptMove(bufferedInput, timingBonus, timing);
            ClearBufferedInput();
        }
        else if (inputBufferTimer <= 0)
        {
            // 缓冲时间用完
            ClearBufferedInput();
            missedInputs++;
            ShowTimingFeedback("缓冲超时", Color.gray);
        }
    }
    
    private void ClearBufferedInput()
    {
        hasBufferedInput = false;
        bufferedInput = Vector2Int.zero;
        inputBufferTimer = 0f;
    }
    
    private void AttemptMove(Vector2Int direction, float timingBonus, RhythmManager.TimingType timing)
    {
        if (gridMovement == null) return;
        
        bool moveSuccessful = gridMovement.TryMove(direction, timingBonus);
        
        if (moveSuccessful)
        {
            // 移动成功，更新统计
            UpdateStats(timing);
            ShowTimingFeedback(GetTimingText(timing), GetTimingColor(timing));
        }
        else
        {
            // 移动失败（可能被阻挡）
            ShowTimingFeedback("被阻挡", Color.yellow);
        }
    }
    
    private float GetTimingBonus(RhythmManager.TimingType timing)
    {
        switch (timing)
        {
            case RhythmManager.TimingType.Perfect:
                return 1.5f; // 完美时机：移动速度加快50%
            case RhythmManager.TimingType.Good:
                return 1.2f; // 良好时机：移动速度加快20%
            default:
                return 1f;   // 其他情况：正常速度
        }
    }
    
    private void UpdateStats(RhythmManager.TimingType timing)
    {
        switch (timing)
        {
            case RhythmManager.TimingType.Perfect:
                perfectHits++;
                break;
            case RhythmManager.TimingType.Good:
                goodHits++;
                break;
        }
    }
    
    private string GetTimingText(RhythmManager.TimingType timing)
    {
        switch (timing)
        {
            case RhythmManager.TimingType.Perfect:
                return "PERFECT!";
            case RhythmManager.TimingType.Good:
                return "Good";
            default:
                return "Miss";
        }
    }
    
    private Color GetTimingColor(RhythmManager.TimingType timing)
    {
        switch (timing)
        {
            case RhythmManager.TimingType.Perfect:
                return Color.yellow;
            case RhythmManager.TimingType.Good:
                return Color.green;
            default:
                return Color.red;
        }
    }
    
    private void ShowTimingFeedback(string text, Color color)
    {
        lastTimingFeedback = text;
        feedbackColor = color;
        feedbackDisplayTime = 1f;
    }
    
    // 节拍事件回调
    private void OnBeat(float beatStrength)
    {
        // 可以在这里添加节拍视觉效果
        // 比如让角色稍微缩放一下表示节拍
    }
    
    private void OnTimingEvaluated(RhythmManager.TimingType timing)
    {
        // 时机评估回调，可以用于额外的效果
    }
    
    void OnDestroy()
    {
        // 取消事件订阅
        if (rhythmManager != null)
        {
            rhythmManager.OnBeat.RemoveListener(OnBeat);
            rhythmManager.OnTimingEvaluated.RemoveListener(OnTimingEvaluated);
        }
    }
} 