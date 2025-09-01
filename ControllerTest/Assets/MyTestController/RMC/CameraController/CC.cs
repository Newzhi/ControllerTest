using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 摄像机控制器 - 实现第三人称摄像机跟随系统
/// </summary>
public class CC : MonoBehaviour
{
    #region 公共变量
    [Header("跟随目标")]
    public Transform target;                    // 跟随目标
    public string targetTag = "Player";         // 目标标签
    
    [Header("跟随参数")]
    public Vector3 offset = new Vector3(0, 2, -5);  // 相对偏移
    public float followSpeed = 5f;              // 跟随速度
    public float rotationSpeed = 3f;            // 旋转速度
    
    [Header("鼠标控制")]
    public bool enableMouseControl = true;      // 启用鼠标控制
    public float mouseSensitivity = 2f;         // 鼠标灵敏度
    public float minVerticalAngle = -30f;       // 最小垂直角度
    public float maxVerticalAngle = 60f;        // 最大垂直角度
    
    [Header("碰撞检测")]
    public bool enableCollision = true;         // 启用碰撞检测
    public LayerMask collisionLayers = -1;      // 碰撞层
    public float collisionRadius = 0.2f;        // 碰撞检测半径
    
    [Header("平滑设置")]
    public bool smoothFollow = true;            // 平滑跟随
    public bool smoothRotation = true;          // 平滑旋转
    public float smoothTime = 0.3f;             // 平滑时间
    #endregion

    #region 私有变量
    private Vector3 currentVelocity;            // 当前速度
    private Vector3 targetPosition;             // 目标位置
    private Quaternion targetRotation;          // 目标旋转
    
    // 鼠标控制相关
    private float currentX = 0f;                // 当前X轴旋转
    private float currentY = 0f;                // 当前Y轴旋转
    private Vector2 mouseInput;                 // 鼠标输入
    
    // 碰撞检测相关
    private Vector3 lastValidPosition;          // 最后有效位置
    private bool isColliding = false;           // 是否碰撞
    
    // 平滑相关
    private Vector3 velocity = Vector3.zero;    // 平滑速度
    #endregion

    #region Unity生命周期
    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        InitializeCamera();
    }

    /// <summary>
    /// 每帧更新
    /// </summary>
    private void Update()
    {
        HandleInput();
        UpdateCameraPosition();
        UpdateCameraRotation();
    }

    /// <summary>
    /// 延迟更新（在角色移动后）
    /// </summary>
    private void LateUpdate()
    {
        if (target != null)
        {
            ApplyCameraTransform();
        }
    }
    #endregion

    #region 初始化系统
    /// <summary>
    /// 初始化摄像机
    /// </summary>
    private void InitializeCamera()
    {
        // 自动查找目标
        if (target == null)
        {
            FindTarget();
        }

        // 设置初始位置
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.LookAt(target);
            
            // 记录初始旋转角度
            Vector3 angles = transform.eulerAngles;
            currentX = angles.x;
            currentY = angles.y;
            
            lastValidPosition = transform.position;
        }

        // 锁定鼠标光标
        if (enableMouseControl)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// 查找跟随目标
    /// </summary>
    private void FindTarget()
    {
        // 按标签查找
        GameObject targetObj = GameObject.FindGameObjectWithTag(targetTag);
        if (targetObj != null)
        {
            target = targetObj.transform;
            return;
        }

        // 按名称查找
        targetObj = GameObject.Find("Character");
        if (targetObj != null)
        {
            target = targetObj.transform;
            return;
        }

        // 查找玩家控制器
        var playerController = FindObjectOfType<MonoBehaviour>();
        if (playerController != null)
        {
            target = playerController.transform;
        }
    }
    #endregion

    #region 输入处理系统
    /// <summary>
    /// 处理输入
    /// </summary>
    private void HandleInput()
    {
        if (!enableMouseControl || target == null) return;

        // 获取鼠标输入
        mouseInput.x = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseInput.y = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 更新旋转角度
        currentY += mouseInput.x;
        currentX -= mouseInput.y;

        // 限制垂直角度
        currentX = Mathf.Clamp(currentX, minVerticalAngle, maxVerticalAngle);

        // ESC键解锁鼠标
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMouseLock();
        }
    }

    /// <summary>
    /// 切换鼠标锁定状态
    /// </summary>
    private void ToggleMouseLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    #endregion

    #region 摄像机位置系统
    /// <summary>
    /// 更新摄像机位置
    /// </summary>
    private void UpdateCameraPosition()
    {
        if (target == null) return;

        // 计算目标位置
        Vector3 desiredPosition = CalculateDesiredPosition();
        
        // 碰撞检测
        if (enableCollision)
        {
            desiredPosition = HandleCollision(desiredPosition);
        }

        targetPosition = desiredPosition;
    }

    /// <summary>
    /// 计算期望位置
    /// </summary>
    /// <returns>期望位置</returns>
    private Vector3 CalculateDesiredPosition()
    {
        // 计算旋转后的偏移
        Quaternion rotation = Quaternion.Euler(currentX, currentY, 0);
        Vector3 rotatedOffset = rotation * offset;
        
        return target.position + rotatedOffset;
    }

    /// <summary>
    /// 处理碰撞检测
    /// </summary>
    /// <param name="desiredPosition">期望位置</param>
    /// <returns>调整后的位置</returns>
    private Vector3 HandleCollision(Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - target.position;
        float distance = direction.magnitude;

        // 射线检测
        RaycastHit hit;
        if (Physics.SphereCast(target.position, collisionRadius, direction.normalized, out hit, distance, collisionLayers))
        {
            // 调整位置到碰撞点前方
            Vector3 adjustedPosition = hit.point + hit.normal * collisionRadius;
            isColliding = true;
            return adjustedPosition;
        }

        isColliding = false;
        return desiredPosition;
    }
    #endregion

    #region 摄像机旋转系统
    /// <summary>
    /// 更新摄像机旋转
    /// </summary>
    private void UpdateCameraRotation()
    {
        if (target == null) return;

        // 计算目标旋转
        if (enableMouseControl)
        {
            targetRotation = Quaternion.Euler(currentX, currentY, 0);
        }
        else
        {
            // 自动朝向目标
            Vector3 direction = target.position - transform.position;
            targetRotation = Quaternion.LookRotation(direction);
        }
    }
    #endregion

    #region 摄像机应用系统
    /// <summary>
    /// 应用摄像机变换
    /// </summary>
    private void ApplyCameraTransform()
    {
        // 应用位置
        if (smoothFollow)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        }

        // 应用旋转
        if (smoothRotation)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }
    #endregion

    #region 公共接口
    /// <summary>
    /// 设置跟随目标
    /// </summary>
    /// <param name="newTarget">新目标</param>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            lastValidPosition = target.position + offset;
        }
    }

    /// <summary>
    /// 设置偏移量
    /// </summary>
    /// <param name="newOffset">新偏移量</param>
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }

    /// <summary>
    /// 设置跟随速度
    /// </summary>
    /// <param name="speed">跟随速度</param>
    public void SetFollowSpeed(float speed)
    {
        followSpeed = speed;
    }

    /// <summary>
    /// 重置摄像机位置
    /// </summary>
    public void ResetCamera()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.LookAt(target);
        }
    }

    /// <summary>
    /// 启用/禁用鼠标控制
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetMouseControl(bool enabled)
    {
        enableMouseControl = enabled;
        if (enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    #endregion

    #region 调试和可视化
    /// <summary>
    /// 绘制调试信息
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        // 绘制跟随线
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);

        // 绘制目标位置
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.1f);

        // 绘制碰撞检测球
        if (enableCollision)
        {
            Gizmos.color = isColliding ? Color.red : Color.blue;
            Gizmos.DrawWireSphere(transform.position, collisionRadius);
        }
    }
    #endregion
}
