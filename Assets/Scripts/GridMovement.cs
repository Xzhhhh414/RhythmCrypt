using System.Collections;
using UnityEngine;

public class GridMovement : MonoBehaviour
{
    [Header("网格设置")]
    public Vector2Int gridPosition = Vector2Int.zero;
    public float gridSize = 1f;
    
    [Header("移动设置")]
    [Range(0.1f, 1f)]
    public float moveDuration = 0.3f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("调试")]
    public bool showGridPosition = true;
    
    // 移动状态
    private bool isMoving = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float moveStartTime;
    
    // 事件
    public System.Action<Vector2Int> OnMoveStart;
    public System.Action<Vector2Int> OnMoveComplete;
    
    // 属性
    public bool IsMoving => isMoving;
    public Vector3 WorldPosition => GridToWorldPosition(gridPosition);
    
    void Start()
    {
        // 初始化位置
        transform.position = WorldPosition;
    }
    
    void Update()
    {
        // 处理移动动画
        if (isMoving)
        {
            UpdateMovement();
        }
    }
    
    void OnGUI()
    {
        if (!showGridPosition) return;
        
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        screenPos.y = Screen.height - screenPos.y; // 翻转Y坐标
        
        GUI.Label(new Rect(screenPos.x - 30, screenPos.y - 40, 60, 20), 
                  $"({gridPosition.x}, {gridPosition.y})");
    }
    
    /// <summary>
    /// 尝试移动到指定方向
    /// </summary>
    public bool TryMove(Vector2Int direction, float timingBonus = 1f)
    {
        if (isMoving) return false;
        
        Vector2Int targetGridPos = gridPosition + direction;
        
        // 检查是否可以移动到目标位置
        if (IsValidPosition(targetGridPos))
        {
            StartMove(targetGridPos, timingBonus);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 直接设置网格位置（瞬间移动）
    /// </summary>
    public void SetGridPosition(Vector2Int newPosition)
    {
        if (isMoving) return;
        
        gridPosition = newPosition;
        transform.position = WorldPosition;
    }
    
    /// <summary>
    /// 开始移动动画
    /// </summary>
    private void StartMove(Vector2Int targetGridPos, float timingBonus)
    {
        isMoving = true;
        startPosition = transform.position;
        targetPosition = GridToWorldPosition(targetGridPos);
        moveStartTime = Time.time;
        
        // 根据时机准确度调整移动时间
        float adjustedDuration = moveDuration / timingBonus;
        
        OnMoveStart?.Invoke(targetGridPos);
        
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
        gridPosition = WorldToGridPosition(targetPosition);
        isMoving = false;
        
        OnMoveComplete?.Invoke(gridPosition);
    }
    
    /// <summary>
    /// 更新移动状态（如果不使用协程的话）
    /// </summary>
    private void UpdateMovement()
    {
        float elapsedTime = Time.time - moveStartTime;
        float progress = elapsedTime / moveDuration;
        
        if (progress >= 1f)
        {
            // 移动完成
            transform.position = targetPosition;
            gridPosition = WorldToGridPosition(targetPosition);
            isMoving = false;
            OnMoveComplete?.Invoke(gridPosition);
        }
        else
        {
            // 继续移动
            float curveValue = moveCurve.Evaluate(progress);
            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
        }
    }
    
    /// <summary>
    /// 检查位置是否有效
    /// </summary>
    private bool IsValidPosition(Vector2Int position)
    {
        // 这里可以添加碰撞检测、边界检查等逻辑
        // 目前暂时允许所有位置
        return true;
    }
    
    /// <summary>
    /// 网格坐标转世界坐标
    /// </summary>
    private Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x * gridSize, gridPos.y * gridSize, transform.position.z);
    }
    
    /// <summary>
    /// 世界坐标转网格坐标
    /// </summary>
    private Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / gridSize),
            Mathf.RoundToInt(worldPos.y / gridSize)
        );
    }
    
    /// <summary>
    /// 获取指定方向的相邻网格位置
    /// </summary>
    public Vector2Int GetAdjacentPosition(Vector2Int direction)
    {
        return gridPosition + direction;
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
            gridPosition = WorldToGridPosition(transform.position);
        }
    }
} 