using System.Collections;
using System.Collections.Generic;
using Animancer.Samples.FineControl;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RMController
{
    /// <summary>
    /// 角色控制器主类 - 基于Root Motion的角色移动系统
    /// </summary>
    public class RMC : MonoBehaviour
    {
        #region 枚举定义
        /// <summary>
        /// 玩家姿态枚举
        /// </summary>
        public enum PlayerPosture
        {
            Crouch,     // 蹲下
            Stand,      // 站立
            Midair,      // 滞空
            Swim
        }

        /// <summary>
        /// 移动状态枚举
        /// </summary>
        public enum LocomotionState
        {
            Idle,       // 待机
            Walk,       // 行走
            Run         // 跑步
        }

        /// <summary>
        /// 瞄准状态枚举
        /// </summary>
        public enum AimState
        {
            Normal,     // 普通状态
            Aim         // 瞄准状态
        }
        #endregion

        #region 公共变量
        [Header("变换引用")]
        public Transform playerTransform;
        public Transform cameraTransform;
        //public Transform interactPoint;

        [Header("状态")]
        public PlayerPosture playerPosture = PlayerPosture.Stand;
        public LocomotionState locomotionState = LocomotionState.Idle;
        public AimState aimState = AimState.Normal;


        [Header("移动参数")]
        public Vector3 playerMovement = Vector3.zero;
        public float crouchSpeed = 1.5f;
        public float walkSpeed = 2.5f;
        public float runSpeed = 5.5f;
        //速度缓存池
        private const int CACHE_SIZE = 3;
        private Vector3[] velCache = new Vector3[CACHE_SIZE];
        private int currentCacheIndex = 0;
        private Vector3 averageVelocity = Vector3.zero;
        
        [Header("跳跃相关")]
        public float jumpVelocity = 5f;
        public float maxHeight = 1f;
        public float fallMultiplier = 1.5f;
        
        [Header("碰撞以及环境检测处理")]
        public Collider[] colliders;
        //地面检测部分
        private bool IsGrounded = false;
        private float groundCheckOffset = 0.1f;

        [Header("物理参数")]
        public float gravity = -9.8f;
        
        [Header("交互参数")]
        public float interactionRange = 1.5f;
        #endregion

        #region 私有变量
        // 组件引用
        private Animator animator;
        private CharacterController characterController;

        // 状态阈值
        private float crouchThreshold = 0f;
        private float standThreshold = 1f;
        private float midairThreshold = 2.1f; //解决动画抖动问题

        // 输入状态
        private Vector2 moveInput;
        private bool isRunning;
        private bool isCrouch;
        private bool isAiming;
        private bool isJumping;

        // 动画参数哈希
        private int postureHash;
        private int moveSpeedHash;
        private int turnSpeedHash;
        private int verticalValHash;

        // 物理状态
        private float verticalVelocity;

        // 交互状态
        //private IInteractable currentInteractable;
        #endregion

        #region Unity生命周期
        /// <summary>
        /// 初始化组件引用和参数
        /// </summary>
        private void Start()
        {
            InitializeComponents();
            InitializeAnimationHashes();
        }

        /// <summary>
        /// 每帧更新逻辑
        /// </summary>
        private void Update()
        {
            CheckGround(); //地面检测
            UpdatePhysics();
            UpdateInput();  //更新输入 后续解耦设计出去 通过中间件通信
            UpdatePlayerState();
            UpdateAnimator();
            //DetectInteractables();
        }
        #endregion

        #region 初始化系统
        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponents()
        {
            characterController = GetComponent<CharacterController>();
            playerTransform = transform;
            animator = GetComponentInChildren<Animator>();
            cameraTransform = Camera.main.transform;
        }

        /// <summary>
        /// 初始化动画参数哈希
        /// </summary>
        private void InitializeAnimationHashes()
        {
            postureHash = Animator.StringToHash("玩家姿态");
            moveSpeedHash = Animator.StringToHash("移动速度");
            turnSpeedHash = Animator.StringToHash("转弯速度");
            verticalValHash = Animator.StringToHash("垂直速度");
        }
        #endregion

        #region 输入处理系统
        /// <summary>
        /// 获取移动输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetMoveInput(InputAction.CallbackContext ctx)
        {
            moveInput = ctx.ReadValue<Vector2>();
        }

        /// <summary>
        /// 获取跑步输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetRunInput(InputAction.CallbackContext ctx)
        {
            isRunning = ctx.ReadValueAsButton();
        }

        /// <summary>
        /// 获取蹲下输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetCrouchInput(InputAction.CallbackContext ctx)
        {
            isCrouch = ctx.ReadValueAsButton();
        }

        /// <summary>
        /// 获取跳跃输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetJumpInput(InputAction.CallbackContext ctx)
        {
            isJumping = ctx.ReadValueAsButton();
        }

        /*
        /// <summary>
        /// 获取交互输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetInteractInput(InputAction.CallbackContext ctx)
        {
            if (ctx.performed && currentInteractable != null)
            {
                currentInteractable.OnInteract(gameObject);
            }
        }
        */
        #endregion

        #region 状态管理系统
        /// <summary>
        /// 更新玩家状态
        /// </summary>
        private void UpdatePlayerState()
        {
            UpdatePostureState();
            UpdateLocomotionState();
        }

        /// <summary>
        /// 更新姿态状态
        /// </summary>
        private void UpdatePostureState()
        {
            if (!IsGrounded)
            {
                playerPosture = PlayerPosture.Midair;
            }
            else if (isCrouch)
            {
                playerPosture = PlayerPosture.Crouch;
            }
            else 
            {
                playerPosture = PlayerPosture.Stand;
            }
        }

        /// <summary>
        /// 更新移动状态
        /// </summary>
        private void UpdateLocomotionState()
        {
            if (moveInput.magnitude == 0)
            {
                locomotionState = LocomotionState.Idle;
            }
            else if (isRunning)
            {
                locomotionState = LocomotionState.Run;
            }
            else
            {
                locomotionState = LocomotionState.Walk;
            }
        }
        #endregion

        #region 碰撞以及环境检测系统

        //地面检测
        void CheckGround()
        {
            Vector3 sphereStart = playerTransform.position + (Vector3.up * groundCheckOffset); 
            
            float detectionDistance = groundCheckOffset - characterController.radius * 2 * characterController.skinWidth; // 15厘米
            
            float sphereRadius = characterController.radius;
            
            if (Physics.SphereCast(sphereStart, sphereRadius, Vector3.down, out RaycastHit hit, detectionDistance))
            {
                Debug.Log($"检测到碰撞: {hit.collider.name} 距离: {hit.distance}");
                IsGrounded = true;
            }
            else
            {
                IsGrounded = false;
            }
        }

        #endregion

        #region 物理系统
        /// <summary>
        /// 更新物理计算
        /// </summary>
        private void UpdatePhysics()
        {
            CalculateGravity();
            HandleJump();
        }

        /// <summary>
        /// 计算重力
        /// </summary>
        private void CalculateGravity()
        {
            if (IsGrounded)
            {
                verticalVelocity = gravity *  Time.deltaTime;
            }
            else
            {
                //根据速度判断是否是下降状态，如果是，使用下降的速度因子平滑速度
                if (verticalVelocity <= 0)
                {
                    verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
                }
                else
                {
                    verticalVelocity += gravity * Time.deltaTime; 
                }

            }
        }

        /// <summary>
        /// 处理跳跃
        /// </summary>
        private void HandleJump()
        {
            if (IsGrounded && isJumping)
            {
                verticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);
                //verticalVelocity = jumpVelocity;
            }
        }
        #endregion

        #region 移动计算系统
        /// <summary>
        /// 更新输入计算
        /// </summary>
        private void UpdateInput()
        {
            CalculateMovementVector();
        }

        /// <summary>
        /// 计算移动向量
        /// </summary>
        private void CalculateMovementVector()
        {
            // 获取摄像机前向投影（忽略Y轴）
            Vector3 camForwardProjection = new Vector3(cameraTransform.forward.x, 0, cameraTransform.forward.z).normalized;
            
            // 计算世界空间移动向量
            Vector3 worldMovement = camForwardProjection * moveInput.y + cameraTransform.right * moveInput.x;
            
            // 转换为角色本地空间
            playerMovement = playerTransform.InverseTransformVector(worldMovement);
        }
        #endregion

        #region 动画系统
        /// <summary>
        /// 更新动画器参数
        /// </summary>
        private void UpdateAnimator()
        {
            UpdatePostureAnimation();
            UpdateMovementAnimation();
            UpdateRotationAnimation();
        }

        /// <summary>
        /// 更新姿态动画
        /// </summary>
        private void UpdatePostureAnimation()
        {
            switch (playerPosture)
            {
                case PlayerPosture.Stand:
                    animator.SetFloat(postureHash, standThreshold, 0.1f, Time.deltaTime);
                    break;
                case PlayerPosture.Crouch:
                    animator.SetFloat(postureHash, crouchThreshold, 0.1f, Time.deltaTime);
                    break;
                case PlayerPosture.Midair://不使用damptime防止抖动
                    animator.SetFloat(postureHash, midairThreshold);
                    animator.SetFloat(verticalValHash, verticalVelocity);
                    break;
            }
        }

        /// <summary>
        /// 更新移动动画
        /// </summary>
        private void UpdateMovementAnimation()
        {
            float moveSpeed = 0f;

            switch (playerPosture)
            {
                case PlayerPosture.Stand:
                    moveSpeed = GetStandingMoveSpeed();
                    break;
                case PlayerPosture.Crouch:
                    moveSpeed = GetCrouchingMoveSpeed();
                    break;
            }

            animator.SetFloat(moveSpeedHash, moveSpeed, 0.1f, Time.deltaTime);
        }

        /// <summary>
        /// 获取站立状态移动速度
        /// </summary>
        /// <returns>移动速度值</returns>
        private float GetStandingMoveSpeed()
        {
            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    return 0f;
                case LocomotionState.Walk:
                    return playerMovement.magnitude * walkSpeed;
                case LocomotionState.Run:
                    return playerMovement.magnitude * runSpeed;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 获取蹲下状态移动速度
        /// </summary>
        /// <returns>移动速度值</returns>
        private float GetCrouchingMoveSpeed()
        {
            return locomotionState == LocomotionState.Idle ? 0f : playerMovement.magnitude * crouchSpeed;
        }

        /// <summary>
        /// 更新旋转动画
        /// </summary>
        private void UpdateRotationAnimation()
        {
            if (moveInput.magnitude > 0.1f)
            {
                // 计算转向角度
                float turnAngle = Mathf.Atan2(playerMovement.x, playerMovement.z);
                
                // 设置动画参数
                animator.SetFloat(turnSpeedHash, turnAngle, 0.1f, Time.deltaTime);
                
                // 应用角色旋转
                playerTransform.Rotate(0, turnAngle * 200f * Time.deltaTime, 0f);
            }
        }
        #endregion

        #region Root Motion移动系统
        
        //计算缓存速度的平均速度
        Vector3 AverageVel(Vector3 newVel)
        {
            velCache[currentCacheIndex] = newVel;
            currentCacheIndex++;
            currentCacheIndex %= CACHE_SIZE;
            Vector3 avgVel = Vector3.zero;
            foreach (Vector3 vel in velCache)
            {
                avgVel += vel;
            }
            return  avgVel / CACHE_SIZE;
        }


        /// <summary>
        /// 动画器移动回调 - 应用Root Motion
        /// </summary>
        private void OnAnimatorMove()
        {
            if (playerPosture != PlayerPosture.Midair)
            {
                Vector3 playerDeltaMovement = animator.deltaPosition;
                playerDeltaMovement.y = verticalVelocity * Time.deltaTime;
                characterController.Move(playerDeltaMovement);
                averageVelocity = AverageVel(animator.velocity);
            }
            else
            {
                Vector3 playerDeltaMovement = averageVelocity * Time.deltaTime;
                playerDeltaMovement.y = verticalVelocity * Time.deltaTime;
                characterController.Move(playerDeltaMovement);
            }

        }
        #endregion

        #region 交互系统
        /*
        /// <summary>
        /// 检测可交互对象
        /// </summary>
        private void DetectInteractables()
        {
            if (interactPoint == null) return;

            // 获取范围内的所有碰撞体
            Collider[] hitColliders = Physics.OverlapSphere(interactPoint.position, interactionRange);

            IInteractable closestInteractable = null;
            float closestDistance = float.MaxValue;

            // 遍历所有碰撞体，找到最近的IInteractable对象
            foreach (Collider hitCollider in hitColliders)
            {
                IInteractable interactable = hitCollider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    float distance = Vector3.Distance(interactPoint.position, hitCollider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestInteractable = interactable;
                    }
                }
            }

            // 更新当前可交互对象
            currentInteractable = closestInteractable;
        }
        #endregion

        #region 公共接口
        /// <summary>
        /// 获取当前移动速度
        /// </summary>
        /// <returns>当前移动速度</returns>
        public float GetCurrentMoveSpeed()
        {
            return playerMovement.magnitude;
        }

        /// <summary>
        /// 检查是否在地面上
        /// </summary>
        /// <returns>是否在地面上</returns>
        public bool IsGrounded()
        {
            return IsGrounded;
        }

        /// <summary>
        /// 获取当前姿态
        /// </summary>
        /// <returns>当前姿态</returns>
        public PlayerPosture GetCurrentPosture()
        {
            return playerPosture;
        }

        /// <summary>
        /// 获取当前移动状态
        /// </summary>
        /// <returns>当前移动状态</returns>
        public LocomotionState GetCurrentLocomotionState()
        {
            return locomotionState;
        }
        */
        #endregion

        #region 编辑器调试
            #if UNITY_EDITOR
        
        [Header("环境检测调试")]
        public bool showGroundCheck = true;
        public Color groundCheckColor = Color.green;
        
        /// <summary>
        /// 绘制地面检测调试信息
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showGroundCheck) return;
            
            // 安全检查：确保组件引用存在
            if (playerTransform == null) playerTransform = transform;
            if (characterController == null) characterController = GetComponent<CharacterController>();
            
            // 如果组件仍然为空，直接返回
            if (playerTransform == null || characterController == null) return;
    
            // 使用与CheckGround()完全一致的计算逻辑
            Vector3 sphereStart = playerTransform.position + (Vector3.up * groundCheckOffset);
            float detectionDistance = groundCheckOffset - characterController.radius * 2 * characterController.skinWidth;
            float sphereRadius = characterController.radius;
    
            // 1. 绘制检测起点（SphereCast起始位置）
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(sphereStart, sphereRadius);
            
            // 添加半透明内部球体
            Gizmos.color = new Color(0, 0, 1, 0.3f); // 半透明蓝色
            Gizmos.DrawSphere(sphereStart, sphereRadius);
    
            // 2. 绘制检测方向线
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(sphereStart, sphereStart + Vector3.down * detectionDistance);
    
            // 3. 绘制检测终点（SphereCast结束位置）
            Vector3 endPosition = sphereStart + Vector3.down * detectionDistance;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(endPosition, sphereRadius);
            
            // 添加半透明内部球体
            Gizmos.color = new Color(1, 1, 0, 0.3f); // 半透明黄色
            Gizmos.DrawSphere(endPosition, sphereRadius);
            
            // 4. 绘制检测路径（显示SphereCast的移动轨迹）
            Gizmos.color = new Color(1, 0, 1, 0.5f); // 半透明紫色
            for (int i = 0; i < 10; i++)
            {
                float t = (float)i / 9f;
                Vector3 pathPoint = Vector3.Lerp(sphereStart, endPosition, t);
                Gizmos.DrawWireSphere(pathPoint, sphereRadius * 0.1f);
            }
        }
        
        /// <summary>
        /// 切换地面检测显示
        /// </summary>
        [ContextMenu("切换地面检测显示")]
        public void ToggleGroundCheckDisplay()
        {
            showGroundCheck = !showGroundCheck;
        }

            #endif
        #endregion
    }
}
