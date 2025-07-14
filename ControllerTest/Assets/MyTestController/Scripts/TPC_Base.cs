using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第三人称角色控制器 
/// 只保留基础的移动、跳跃功能
/// </summary>
public class TPC_Base : MonoBehaviour
{
    #region 基础组件
    [Header("基础组件")]
    [SerializeField] protected Transform playerTransform;        // 玩家变换组件
    [SerializeField] protected Animator animator;                // 动画器组件
    [SerializeField] protected Transform cameraTransform;        // 相机变换组件
    [SerializeField] protected CharacterController characterController;  // 角色控制器组件
    #endregion

    #region 玩家姿态状态
    /// <summary>
    /// 玩家姿态枚举
    /// </summary>
    public enum PlayerPosture
    {
        Crouch,     // 蹲下
        Stand,      // 站立
        Falling,    // 下落
        Jumping,    // 跳跃
        Landing     // 着陆
    }
    
    [HideInInspector] public PlayerPosture playerPosture = PlayerPosture.Stand;  // 当前姿态状态

    // 动画参数阈值（用于设置动画器中的姿态参数）
    protected const float CROUCH_THRESHOLD = 0f;     // 蹲下阈值
    protected const float STAND_THRESHOLD = 1f;      // 站立阈值
    protected const float MIDAIR_THRESHOLD = 2.1f;   // 空中阈值
    protected const float LANDING_THRESHOLD = 1f;    // 着陆阈值
    #endregion

    #region 移动状态
    /// <summary>
    /// 移动状态枚举
    /// </summary>
    public enum LocomotionState
    {
        Idle,   // 待机
        Walk,   // 行走
        Run     // 奔跑
    }
    
    [HideInInspector] public LocomotionState locomotionState = LocomotionState.Idle;  // 当前移动状态
    #endregion

    #region 移动速度参数
    [Header("移动速度设置")]
    [SerializeField] protected float crouchSpeed = 1.5f;    // 蹲下移动速度
    [SerializeField] protected float walkSpeed = 2.5f;      // 行走速度
    [SerializeField] protected float runSpeed = 5.5f;       // 奔跑速度
    [SerializeField] protected float rotationSpeed = 200f;  // 旋转速度
    #endregion

    #region 输入状态
    protected Vector2 moveInput;      // 移动输入值（WASD）
    protected bool isRunPressed;      // 是否按下奔跑键
    protected bool isCrouchPressed;   // 是否按下蹲下键
    protected bool isJumpPressed;     // 是否按下跳跃键
    #endregion

    #region 动画器参数哈希值
    protected int postureHash;        // 姿态参数哈希值
    protected int moveSpeedHash;      // 移动速度参数哈希值
    protected int turnSpeedHash;      // 转向速度参数哈希值
    protected int verticalVelHash;    // 垂直速度参数哈希值
    #endregion

    #region 移动相关
    protected Vector3 playerMovementWorldSpace = Vector3.zero;  // 世界空间移动向量
    protected Vector3 playerMovement = Vector3.zero;            // 本地空间移动向量
    #endregion

    #region 重力与跳跃
    [Header("重力与跳跃设置")]
    [SerializeField] protected float gravity = -9.8f;           // 重力大小
    [SerializeField] protected float maxHeight = 1.5f;          // 最大跳跃高度
    [SerializeField] protected float fallMultiplier = 1.5f;     // 下落加速度倍数
    [SerializeField] protected float jumpCD = 0.15f;            // 跳跃冷却时间
    
    protected float verticalVelocity;    // 垂直速度
    protected bool isGrounded;           // 是否在地面
    protected bool couldFall;            // 是否可以跌落
    protected bool isLanding;            // 是否正在着陆
    protected float fallHeight = 0.5f;   // 跌落检测高度
    protected float groundCheckOffset = 0.5f;  // 地面检测射线偏移量
    #endregion

    #region Unity生命周期
    /// <summary>
    /// 初始化方法
    /// </summary>
    protected virtual void Start()
    {
        InitializeComponents();
        InitializeAnimatorHashes();
        SetupCursor();
    }

    /// <summary>
    /// 每帧更新方法
    /// </summary>
    protected virtual void Update()
    {
        CheckGround();           // 检测地面
        SwitchPlayerStates();    // 切换玩家状态
        CalculateGravity();      // 计算重力
        HandleJump();           // 处理跳跃
        CalculateInputDirection(); // 计算输入方向
        SetupAnimator();        // 设置动画器参数
    }
    #endregion

    #region 初始化方法
    /// <summary>
    /// 初始化组件引用
    /// </summary>
    protected virtual void InitializeComponents()
    {
        // 获取或设置组件引用
        if (playerTransform == null)
            playerTransform = transform;
        if (animator == null)
            animator = GetComponent<Animator>();
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 初始化动画器参数哈希值
    /// </summary>
    protected virtual void InitializeAnimatorHashes()
    {
        // 将动画器参数名称转换为哈希值，提高性能
        postureHash = Animator.StringToHash("玩家姿态");
        moveSpeedHash = Animator.StringToHash("移动速度");
        turnSpeedHash = Animator.StringToHash("转弯速度");
        verticalVelHash = Animator.StringToHash("垂直速度");
    }

    /// <summary>
    /// 设置鼠标光标状态
    /// </summary>
    protected virtual void SetupCursor()
    {
        // 锁定鼠标光标到屏幕中心
        Cursor.lockState = CursorLockMode.Locked;
    }
    #endregion

    #region 输入处理
    /// <summary>
    /// 处理移动输入
    /// </summary>
    public virtual void GetMoveInput(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    /// <summary>
    /// 处理奔跑输入
    /// </summary>
    public virtual void GetRunInput(InputAction.CallbackContext ctx)
    {
        isRunPressed = ctx.ReadValueAsButton();
    }

    /// <summary>
    /// 处理蹲下输入
    /// </summary>
    public virtual void GetCrouchInput(InputAction.CallbackContext ctx)
    {
        isCrouchPressed = ctx.ReadValueAsButton();
    }

    /// <summary>
    /// 处理跳跃输入
    /// </summary>
    public virtual void GetJumpInput(InputAction.CallbackContext ctx)
    {
        isJumpPressed = ctx.ReadValueAsButton();
    }
    #endregion

    #region 状态管理
    /// <summary>
    /// 切换玩家各种状态
    /// </summary>
    protected virtual void SwitchPlayerStates()
    {
        // 根据当前姿态和输入切换状态
        switch (playerPosture)
        {
            case PlayerPosture.Stand:
                // 站立状态：可以切换到跳跃、下落、蹲下
                if (verticalVelocity > 0)
                {
                    playerPosture = PlayerPosture.Jumping;
                }
                else if (!isGrounded && couldFall)
                {
                    playerPosture = PlayerPosture.Falling;
                }
                else if (isCrouchPressed)
                {
                    playerPosture = PlayerPosture.Crouch;
                }
                break;

            case PlayerPosture.Crouch:
                // 蹲下状态：可以切换到下落或站立
                if (!isGrounded && couldFall)
                {
                    playerPosture = PlayerPosture.Falling;
                }
                else if (!isCrouchPressed)
                {
                    playerPosture = PlayerPosture.Stand;
                }
                break;

            case PlayerPosture.Falling:
                // 下落状态：着地后切换到着陆
                if (isGrounded)
                {
                    StartCoroutine(CoolDownJump());
                }
                if (isLanding)
                {
                    playerPosture = PlayerPosture.Landing;
                }
                break;

            case PlayerPosture.Jumping:
                // 跳跃状态：着地后切换到着陆
                if (isGrounded)
                {
                    StartCoroutine(CoolDownJump());
                }
                if (isLanding)
                {
                    playerPosture = PlayerPosture.Landing;
                }
                break;

            case PlayerPosture.Landing:
                // 着陆状态：着陆完成后切换到站立
                if (!isLanding)
                {
                    playerPosture = PlayerPosture.Stand;
                }
                break;
        }

        // 更新移动状态
        UpdateLocomotionState();
    }

    /// <summary>
    /// 更新移动状态
    /// </summary>
    protected virtual void UpdateLocomotionState()
    {
        if (moveInput.magnitude == 0)
        {
            locomotionState = LocomotionState.Idle;
        }
        else if (!isRunPressed)
        {
            locomotionState = LocomotionState.Walk;
        }
        else
        {
            locomotionState = LocomotionState.Run;
        }
    }
    #endregion

    #region 地面检测
    /// <summary>
    /// 检测玩家是否在地面
    /// </summary>
    protected virtual void CheckGround()
    {
        // 使用球体射线检测地面
        if (Physics.SphereCast(
            playerTransform.position + (Vector3.up * groundCheckOffset), 
            characterController.radius, 
            Vector3.down, 
            out RaycastHit hit, 
            groundCheckOffset - characterController.radius + 2 * characterController.skinWidth))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
            // 检测是否可以跌落（下方是否有地面）
            couldFall = !Physics.Raycast(playerTransform.position, Vector3.down, fallHeight);
        }
    }

    /// <summary>
    /// 跳跃冷却协程
    /// </summary>
    protected virtual IEnumerator CoolDownJump()
    {
        // 根据着陆速度计算着陆动画参数
        float landingThreshold = Mathf.Clamp(verticalVelocity, -10, 0);
        landingThreshold /= 20f;
        landingThreshold += 1f;
        
        isLanding = true;
        playerPosture = PlayerPosture.Landing;
        
        // 等待跳跃冷却时间
        yield return new WaitForSeconds(jumpCD);
        isLanding = false;
    }
    #endregion

    #region 重力与跳跃
    /// <summary>
    /// 计算重力影响
    /// </summary>
    protected virtual void CalculateGravity()
    {
        // 根据当前状态计算重力
        if (playerPosture != PlayerPosture.Jumping && playerPosture != PlayerPosture.Falling)
        {
            // 非跳跃/下落状态
            if (!isGrounded)
            {
                // 在空中时应用重力
                verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            }
            else
            {
                // 在地面时保持轻微向下力
                verticalVelocity = gravity * Time.deltaTime;
            }
        }
        else
        {
            // 跳跃/下落状态
            if (verticalVelocity <= 0 || !isJumpPressed)
            {
                // 下落时应用更大的重力
                verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            }
            else
            {
                // 上升时应用正常重力
                verticalVelocity += gravity * Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// 处理跳跃逻辑
    /// </summary>
    protected virtual void HandleJump()
    {
        // 只有在站立状态且按下跳跃键时才跳跃
        if (playerPosture == PlayerPosture.Stand && isJumpPressed)
        {
            // 根据移动状态调整跳跃速度
            float velOffset = 0f;
            switch (locomotionState)
            {
                case LocomotionState.Run:
                    velOffset = 1f;
                    break;
                case LocomotionState.Walk:
                    velOffset = 0.5f;
                    break;
                case LocomotionState.Idle:
                    velOffset = 0f;
                    break;
            }

            // 计算跳跃初速度（使用物理公式：v = sqrt(2 * g * h)）
            verticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);
            
            // 可以在这里添加跳跃音效
            OnJump();
        }
    }

    /// <summary>
    /// 跳跃事件回调（子类可以重写添加特效或音效）
    /// </summary>
    protected virtual void OnJump()
    {
        // 子类可以重写此方法添加跳跃特效或音效
        Debug.Log("玩家跳跃！");
    }
    #endregion

    #region 移动计算
    /// <summary>
    /// 计算输入相对于相机的方向
    /// </summary>
    protected virtual void CalculateInputDirection()
    {
        // 获取相机前方向量（忽略Y轴）
        Vector3 camForwardProjection = new Vector3(cameraTransform.forward.x, 0, cameraTransform.forward.z).normalized;
        
        // 计算世界空间移动向量
        playerMovementWorldSpace = camForwardProjection * moveInput.y + cameraTransform.right * moveInput.x;
        
        // 转换为本地空间移动向量
        playerMovement = playerTransform.InverseTransformVector(playerMovementWorldSpace);
    }
    #endregion

    #region 动画系统
    /// <summary>
    /// 设置动画器参数
    /// </summary>
    protected virtual void SetupAnimator()
    {
        // 根据当前姿态设置动画参数
        switch (playerPosture)
        {
            case PlayerPosture.Stand:
                SetStandingAnimation();
                break;
            case PlayerPosture.Crouch:
                SetCrouchingAnimation();
                break;
            case PlayerPosture.Jumping:
                SetJumpingAnimation();
                break;
            case PlayerPosture.Landing:
                SetLandingAnimation();
                break;
            case PlayerPosture.Falling:
                SetFallingAnimation();
                break;
        }

        // 设置转向动画
        SetRotationAnimation();
    }

    /// <summary>
    /// 设置站立状态动画
    /// </summary>
    protected virtual void SetStandingAnimation()
    {
        animator.SetFloat(postureHash, STAND_THRESHOLD, 0.1f, Time.deltaTime);
        
        // 根据移动状态设置移动速度
        switch (locomotionState)
        {
            case LocomotionState.Idle:
                animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                break;
            case LocomotionState.Walk:
                animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                break;
            case LocomotionState.Run:
                animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                break;
        }
    }

    /// <summary>
    /// 设置蹲下状态动画
    /// </summary>
    protected virtual void SetCrouchingAnimation()
    {
        animator.SetFloat(postureHash, CROUCH_THRESHOLD, 0.1f, Time.deltaTime);
        
        switch (locomotionState)
        {
            case LocomotionState.Idle:
                animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                break;
            default:
                animator.SetFloat(moveSpeedHash, playerMovement.magnitude * crouchSpeed, 0.1f, Time.deltaTime);
                break;
        }
    }

    /// <summary>
    /// 设置跳跃状态动画
    /// </summary>
    protected virtual void SetJumpingAnimation()
    {
        animator.SetFloat(postureHash, MIDAIR_THRESHOLD);
        animator.SetFloat(verticalVelHash, verticalVelocity);
    }

    /// <summary>
    /// 设置着陆状态动画
    /// </summary>
    protected virtual void SetLandingAnimation()
    {
        animator.SetFloat(postureHash, LANDING_THRESHOLD, 0.03f, Time.deltaTime);
        
        switch (locomotionState)
        {
            case LocomotionState.Idle:
                animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                break;
            case LocomotionState.Walk:
                animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                break;
            case LocomotionState.Run:
                animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                break;
        }
    }

    /// <summary>
    /// 设置下落状态动画
    /// </summary>
    protected virtual void SetFallingAnimation()
    {
        animator.SetFloat(postureHash, MIDAIR_THRESHOLD);
        animator.SetFloat(verticalVelHash, verticalVelocity);
    }

    /// <summary>
    /// 设置转向动画
    /// </summary>
    protected virtual void SetRotationAnimation()
    {
        // 只有在非跳跃状态下才进行转向
        if (playerPosture != PlayerPosture.Jumping)
        {
            // 计算转向角度
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
            
            // 旋转玩家
            playerTransform.Rotate(0, rad * rotationSpeed * Time.deltaTime, 0f);
        }
    }
    #endregion

    #region 物理移动
    /// <summary>
    /// 动画器移动回调
    /// </summary>
    protected virtual void OnAnimatorMove()
    {
        // 根据当前状态处理移动
        if (playerPosture != PlayerPosture.Jumping && playerPosture != PlayerPosture.Falling)
        {
            // 正常地面移动：使用动画器的根运动
            characterController.enabled = true;
            Vector3 playerDeltaMovement = animator.deltaPosition;
            playerDeltaMovement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
        }
        else
        {
            // 跳跃/下落移动：使用物理计算
            characterController.enabled = true;
            Vector3 playerDeltaMovement = new Vector3(0, verticalVelocity, 0) * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
        }
    }
    #endregion

    #region 调试
    /// <summary>
    /// 绘制调试信息
    /// </summary>
    protected virtual void OnDrawGizmos()
    {
        // 绘制地面检测范围
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerTransform.position + Vector3.up * groundCheckOffset, 
                characterController != null ? characterController.radius : 0.5f);
        }
    }
    #endregion
}
