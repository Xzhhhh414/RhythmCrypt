using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RhythmPlayerController : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("节拍管理器，留空则自动查找")]
    public RhythmManager rhythmManager;
    

    
    // 网格大小（由GameManager设置，不在Inspector中显示）
    [System.NonSerialized]
    public float gridSize = 1f;
    
    [Header("移动设置")]
    [Range(0.1f, 1f)]
    public float moveDuration = 0.3f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("控制设置")]
    public bool requireBeatTiming = true;  // 是否要求按节拍输入
    public bool allowEarlyInput = true;    // 是否允许提前输入
    
    [Header("输入缓冲")]
    [Range(0f, 0.5f)]
    public float inputBufferTime = 0.1f;   // 输入缓冲时间
    
    [Header("反馈设置")]
    public bool showTimingFeedback = true;
    
    [Header("输入设置")]
    [Tooltip("输入动作资源，留空则使用默认输入（WASD+方向键+手柄）")]
    public InputActionAsset inputActions;
    
    [Header("调试")]
    public bool showCollisionBounds = false;
    
    // Input Actions
    private InputAction moveAction;
    private InputAction toggleBeatModeAction;
    
    // 移动状态
    private bool isMoving = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float moveStartTime;
    
    // 输入状态
    private Vector2Int bufferedInput = Vector2Int.zero;
    private float inputBufferTimer = 0f;
    private bool hasBufferedInput = false;
    
    // 输入检测状态
    private Vector2Int lastInputDirection = Vector2Int.zero;
    private bool inputPressed = false;
    
    // 节拍窗口跟踪
    private bool wasInActionWindow = false;
    private bool hadInputInCurrentWindow = false;
    private bool hadSuccessfulActionInCurrentWindow = false;  // 跟踪是否有成功的操作
    private bool isFirstBeat = true;  // 跟踪是否是第一个节拍
    
    // 统计信息
    public int goodHits = 0;
    public int missedInputs = 0;
    
    // 反馈显示
    private string lastTimingFeedback = "";
    private float feedbackDisplayTime = 0f;
    private Color feedbackColor = Color.white;
    
    // 事件
    public System.Action<Vector3> OnMoveStart;
    public System.Action<Vector3> OnMoveComplete;
    
    // 属性
    public bool IsMoving => isMoving;
    
    void Start()
    {
        // 自动获取组件引用
        if (rhythmManager == null)
            rhythmManager = FindObjectOfType<RhythmManager>();
            
        // 初始化Input System
        InitializeInputSystem();
            
        // 订阅节拍事件
        if (rhythmManager != null)
        {
            rhythmManager.OnBeat.AddListener(OnBeat);
            rhythmManager.OnTimingEvaluated.AddListener(OnTimingEvaluated);
        }
        
        // 检查必要组件
        if (rhythmManager == null)
            Debug.LogError("RhythmPlayerController: 找不到RhythmManager!");
    }
    
    private void InitializeInputSystem()
    {
        // 如果没有指定InputActionAsset，创建默认的输入动作
        if (inputActions == null)
        {
            CreateDefaultInputActions();
        }
        else
        {
            // 使用指定的InputActionAsset
            moveAction = inputActions.FindAction("Move");
            toggleBeatModeAction = inputActions.FindAction("ToggleBeatMode");
        }
        
        // 启用输入动作
        if (moveAction != null)
        {
            moveAction.Enable();
        }
        
        if (toggleBeatModeAction != null)
        {
            toggleBeatModeAction.Enable();
            toggleBeatModeAction.performed += OnToggleBeatMode;
        }
        
        Debug.Log("Input System 初始化完成");
    }
    
    private void CreateDefaultInputActions()
    {
        // 创建默认的移动输入动作
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s") 
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow")
            .With("Up", "<Gamepad>/leftStick/up")
            .With("Down", "<Gamepad>/leftStick/down")
            .With("Left", "<Gamepad>/leftStick/left")
            .With("Right", "<Gamepad>/leftStick/right")
            .With("Up", "<Gamepad>/dpad/up")
            .With("Down", "<Gamepad>/dpad/down")
            .With("Left", "<Gamepad>/dpad/left")
            .With("Right", "<Gamepad>/dpad/right");
            
        // 创建切换节拍模式的输入动作
        toggleBeatModeAction = new InputAction("ToggleBeatMode", InputActionType.Button);
        toggleBeatModeAction.AddBinding("<Keyboard>/space");
        toggleBeatModeAction.AddBinding("<Gamepad>/buttonSouth"); // A按钮 (Xbox) / X按钮 (PlayStation)
        
        Debug.Log("创建默认Input Actions");
    }
    
    private void TrackActionWindow()
    {
        if (rhythmManager == null) return;
        
        bool currentlyInActionWindow = rhythmManager.IsInActionWindow();
        
        // 检测进入行动窗口
        if (currentlyInActionWindow && !wasInActionWindow)
        {
            // 刚进入行动窗口，重置所有标记
            hadInputInCurrentWindow = false;
            hadSuccessfulActionInCurrentWindow = false;
        }
        // 检测离开行动窗口
        else if (!currentlyInActionWindow && wasInActionWindow)
        {
            // 刚离开行动窗口，检查这个节拍区间的结果
            // 但跳过第一个节拍的检查
            if (!isFirstBeat && !hadSuccessfulActionInCurrentWindow)
            {
                // 这个节拍区间内没有成功操作，计为错过（不论是否有输入尝试）
                missedInputs++;
                if (!hadInputInCurrentWindow)
                {
                    ShowTimingFeedback("错过机会", Color.gray);
                }
                else
                {
                    ShowTimingFeedback("时机错误", Color.red);
                }
            }
            
            // 第一个节拍结束后，取消第一节拍标记
            if (isFirstBeat)
            {
                isFirstBeat = false;
            }
        }
        
        wasInActionWindow = currentlyInActionWindow;
    }
    
    void Update()
    {
        // 处理移动动画
        if (isMoving)
        {
            UpdateMovement();
        }
        
        // 跟踪行动窗口状态（仅在节拍模式下）
        if (requireBeatTiming)
        {
            TrackActionWindow();
        }
        
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
        // 放大字体
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 24;
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 24;
        
        GUIStyle feedbackStyle = new GUIStyle(GUI.skin.label);
        feedbackStyle.fontSize = 32;  // 反馈文字更大一些
        feedbackStyle.fontStyle = FontStyle.Bold;
        

        
        // 显示统计信息 (放大)
        GUI.Box(new Rect(Screen.width - 400, 20, 360, 160), "玩家统计", boxStyle);
        GUI.Label(new Rect(Screen.width - 380, 70, 340, 40), $"成功: {goodHits}", labelStyle);
        GUI.Label(new Rect(Screen.width - 380, 110, 340, 40), $"错过: {missedInputs}", labelStyle);
        
        int totalInputs = goodHits + missedInputs;
        float accuracy = totalInputs > 0 ? (float)goodHits / totalInputs * 100f : 0f;
        GUI.Label(new Rect(Screen.width - 380, 150, 340, 40), $"准确率: {accuracy:F1}%", labelStyle);
        
        // 显示时机反馈 (放大)
        if (showTimingFeedback && feedbackDisplayTime > 0)
        {
            Vector3 playerScreenPos = Camera.main.WorldToScreenPoint(transform.position);
            playerScreenPos.y = Screen.height - playerScreenPos.y + 60;
            
            GUI.color = feedbackColor;
            GUI.Label(new Rect(playerScreenPos.x - 100, playerScreenPos.y, 200, 60), lastTimingFeedback, feedbackStyle);
            GUI.color = Color.white;
        }
        
        // 显示控制说明 (放大)
        GUI.Box(new Rect(20, Screen.height - 280, 400, 240), "控制说明", boxStyle);
        GUI.Label(new Rect(40, Screen.height - 230, 360, 40), "WASD / 方向键: 移动", labelStyle);
        GUI.Label(new Rect(40, Screen.height - 190, 360, 40), "按节拍输入获得加成", labelStyle);
        GUI.Label(new Rect(40, Screen.height - 150, 360, 40), $"需要节拍: {(requireBeatTiming ? "是" : "否")}", labelStyle);
        GUI.Label(new Rect(40, Screen.height - 110, 360, 40), "空格: 切换节拍要求", labelStyle);
        GUI.Label(new Rect(40, Screen.height - 70, 360, 40), "错过: 时机不对 + 未操作", labelStyle);
    }
    
    private void HandleInput()
    {
        // 获取输入方向
        Vector2Int currentInputDirection = GetInputDirection();
        
        // 检测输入变化（模拟按下事件）
        bool newInputDetected = false;
        if (currentInputDirection != Vector2Int.zero && currentInputDirection != lastInputDirection)
        {
            newInputDetected = true;
        }
        else if (currentInputDirection != Vector2Int.zero && !inputPressed)
        {
            newInputDetected = true;
        }
        
        // 更新输入状态
        inputPressed = currentInputDirection != Vector2Int.zero;
        lastInputDirection = currentInputDirection;
        
        // 处理新的输入
        if (newInputDetected)
        {
            // 标记在当前窗口内有输入（仅在节拍模式下）
            if (requireBeatTiming && rhythmManager != null)
            {
                hadInputInCurrentWindow = true;
            }
            
            if (requireBeatTiming)
            {
                // 需要按节拍输入
                HandleRhythmInput(currentInputDirection);
            }
            else
            {
                // 自由移动模式
                AttemptMove(currentInputDirection, 1f, RhythmManager.TimingType.Good);
            }
        }
    }
    
    private Vector2Int GetInputDirection()
    {
        if (moveAction == null) return Vector2Int.zero;
        
        // 读取2D向量输入
        Vector2 inputVector = moveAction.ReadValue<Vector2>();
        
        // 转换为离散的方向（只允许4个方向）
        Vector2Int direction = Vector2Int.zero;
        
        // 使用阈值来判断输入方向，避免手柄轻微移动触发
        float threshold = 0.5f;
        
        if (Mathf.Abs(inputVector.x) > Mathf.Abs(inputVector.y))
        {
            // 水平移动优先
            if (inputVector.x > threshold)
                direction = Vector2Int.right;
            else if (inputVector.x < -threshold)
                direction = Vector2Int.left;
        }
        else
        {
            // 垂直移动优先
            if (inputVector.y > threshold)
                direction = Vector2Int.up;
            else if (inputVector.y < -threshold)
                direction = Vector2Int.down;
        }
        
        return direction;
    }
    
    private void OnToggleBeatMode(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            requireBeatTiming = !requireBeatTiming;
            Debug.Log($"节拍要求: {(requireBeatTiming ? "开启" : "关闭")}");
        }
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
            // 输入时机不对，但不立即计错过，等节拍结束时统一处理
            ShowTimingFeedback("时机不对", Color.red);
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
            // 缓冲时间用完，但不计错过（在节拍结束时统一处理）
            ClearBufferedInput();
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
        if (isMoving) return;
        
        // 计算目标世界位置
        Vector3 targetWorldPos = transform.position + new Vector3(direction.x * gridSize, direction.y * gridSize, 0);
        
        // 检查是否可以移动到目标位置
        bool moveSuccessful = IsValidPosition(targetWorldPos);
        
        if (moveSuccessful)
        {
            // 移动成功，开始移动动画
            StartMove(targetWorldPos, timingBonus);
            UpdateStats(timing);
            ShowTimingFeedback(GetTimingText(timing), GetTimingColor(timing));
        }
        else
        {
            // 移动失败（被阻挡），但仍然算作成功响应节拍
            UpdateStats(timing);
            ShowTimingFeedback("被阻挡", Color.yellow);
        }
        
        // 无论移动是否成功，都标记为成功响应了节拍
        if (requireBeatTiming)
        {
            hadSuccessfulActionInCurrentWindow = true;
        }
    }
    
    /// <summary>
    /// 检查位置是否有效
    /// </summary>
    private bool IsValidPosition(Vector3 targetWorldPos)
    {
        // 获取自身的Collider2D组件
        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider == null)
        {
            Debug.LogError($"RhythmPlayerController: {gameObject.name} 缺少Collider2D组件！移动功能需要Collider2D进行碰撞检测。");
            return false; // 没有碰撞体则不允许移动
        }
        
        // 计算Collider2D在目标位置的中心点
        Vector2 colliderCenter = targetWorldPos + (Vector3)ownCollider.offset;
        
        // 使用OverlapPoint检测碰撞体中心位置是否有其他碰撞体
        Collider2D hit = Physics2D.OverlapPoint(colliderCenter);
        
        // 如果碰撞到的是自身，则认为位置有效
        return hit == null || hit == ownCollider;
    }
    
    /// <summary>
    /// 开始移动动画
    /// </summary>
    private void StartMove(Vector3 targetWorldPos, float timingBonus)
    {
        isMoving = true;
        startPosition = transform.position;
        targetPosition = targetWorldPos;
        moveStartTime = Time.time;
        
        // 根据时机准确度调整移动时间
        float adjustedDuration = moveDuration / timingBonus;
        
        OnMoveStart?.Invoke(targetWorldPos);
        
        StartCoroutine(MovementCoroutine(adjustedDuration));
    }
    
    /// <summary>
    /// 移动协程
    /// </summary>
    private IEnumerator MovementCoroutine(float duration)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            // 使用动画曲线进行插值
            float curveValue = moveCurve.Evaluate(progress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            
            yield return null;
        }
        
        // 确保最终位置正确
        transform.position = targetPosition;
        isMoving = false;
        
        OnMoveComplete?.Invoke(targetPosition);
    }
    
    /// <summary>
    /// 更新移动状态（备用方法，当前使用协程）
    /// </summary>
    private void UpdateMovement()
    {
        // 注意：当前使用协程进行移动，这个方法作为备用
        float elapsedTime = Time.time - moveStartTime;
        float progress = elapsedTime / moveDuration;
        
        if (progress >= 1f)
        {
            // 移动完成
            transform.position = targetPosition;
            isMoving = false;
            OnMoveComplete?.Invoke(targetPosition);
        }
        else
        {
            // 继续移动
            float curveValue = moveCurve.Evaluate(progress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
        }
    }
    
    /// <summary>
    /// 直接设置位置（瞬间移动）
    /// </summary>
    public void SetPosition(Vector3 newPosition)
    {
        if (isMoving) return;
        
        transform.position = newPosition;
    }
    

    

    

    
    /// <summary>
    /// 停止当前移动
    /// </summary>
    public void StopMovement()
    {
        if (isMoving)
        {
            StopAllCoroutines();
            isMoving = false;
            // 注意：移动中断后可能需要重新同步位置
            // 如果需要精确同步，可以重新实现世界坐标转网格坐标的逻辑
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
    
    /// <summary>
    /// 绘制调试信息
    /// </summary>
    void OnDrawGizmos()
    {
        if (showCollisionBounds)
        {
            // 获取自身Collider2D的大小和偏移
            Collider2D ownCollider = GetComponent<Collider2D>();
            Vector2 debugSize = Vector2.one; // 默认大小
            Vector2 colliderOffset = Vector2.zero;
            
            if (ownCollider != null)
            {
                colliderOffset = ownCollider.offset;
                
                if (ownCollider is BoxCollider2D boxCollider)
                {
                    debugSize = boxCollider.size;
                }
                else if (ownCollider is CircleCollider2D circleCollider)
                {
                    float diameter = circleCollider.radius * 2f;
                    debugSize = new Vector2(diameter, diameter);
                }
                // 可以根据需要添加其他类型的Collider2D
            }
            
            // 绘制当前位置的碰撞检测区域（考虑Collider2D的offset）
            Gizmos.color = Color.green;
            Vector3 currentWorldPos = transform.position + (Vector3)colliderOffset;
            Gizmos.DrawWireCube(currentWorldPos, debugSize);
            
            // 如果正在移动，也绘制目标位置的碰撞检测区域
            if (isMoving)
            {
                Gizmos.color = Color.yellow;
                Vector3 targetColliderPos = targetPosition + (Vector3)colliderOffset;
                Gizmos.DrawWireCube(targetColliderPos, debugSize);
            }
        }
    }
    
    void OnDestroy()
    {
        // 清理Input System
        if (moveAction != null)
        {
            moveAction.Disable();
            moveAction.Dispose();
        }
        
        if (toggleBeatModeAction != null)
        {
            toggleBeatModeAction.performed -= OnToggleBeatMode;
            toggleBeatModeAction.Disable();
            toggleBeatModeAction.Dispose();
        }
        
        // 取消事件订阅
        if (rhythmManager != null)
        {
            rhythmManager.OnBeat.RemoveListener(OnBeat);
            rhythmManager.OnTimingEvaluated.RemoveListener(OnTimingEvaluated);
        }
    }
    
    // 重置统计和状态
    public void ResetStats()
    {
        goodHits = 0;
        missedInputs = 0;
        
        // 重置节拍窗口跟踪状态
        wasInActionWindow = false;
        hadInputInCurrentWindow = false;
        hadSuccessfulActionInCurrentWindow = false;
        isFirstBeat = true;
        
        // 清空时机反馈
        lastTimingFeedback = "";
        feedbackDisplayTime = 0f;
        feedbackColor = Color.white;
        
        // 清空缓冲输入
        ClearBufferedInput();
    }
} 