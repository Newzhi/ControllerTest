using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using Animancer;
using System;
using UnityEngine.InputSystem;

namespace MyTestController.Scripts
{
    /// <summary>
    /// 角色状态枚举 - 扩展版本
    /// </summary>
    public enum CharacterState
    {
        Default,
        Idle,
        Walking,
        Running,
        Sprinting,
        Jumping,
        Falling,
        Landing,
        Swimming,
        Crouching,
        Stunned,
        InVehicle
    }

    /// <summary>
    /// 动画状态枚举 - 对应具体的动画片段
    /// </summary>
    public enum AnimationState
    {
        Idle,
        Walk_Forward, Walk_Backward, Walk_Left, Walk_Right,
        Walk_ForwardLeft, Walk_ForwardRight, Walk_BackwardLeft, Walk_BackwardRight,
        Run_Forward, Run_Backward, Run_Left, Run_Right,
        Run_ForwardLeft, Run_ForwardRight, Run_BackwardLeft, Run_BackwardRight,
        Sprint_Forward, Sprint_Backward, Sprint_Left, Sprint_Right,
        Sprint_ForwardLeft, Sprint_ForwardRight, Sprint_BackwardLeft, Sprint_BackwardRight,
        Jump_Start, Jump_Loop, Jump_Land,
        Fall_Start, Fall_Loop, Fall_Land,
        Swim_Idle, Swim_Forward, Swim_Backward, Swim_Left, Swim_Right,
        Swim_ForwardLeft, Swim_ForwardRight, Swim_BackwardLeft, Swim_BackwardRight,
        Crouch_Idle, Crouch_Walk_Forward, Crouch_Walk_Backward,
        Vehicle_Idle, Vehicle_Drive,
        Turn_Left, Turn_Right, Stop_Forward, Stop_Backward
    }

    /// <summary>
    /// 朝向方法枚举
    /// </summary>
    public enum OrientationMethod
    {
        TowardsCamera,   // 朝向摄像机
        TowardsMovement, // 朝向移动方向
    }

    /// <summary>
    /// 玩家角色输入数据结构
    /// </summary>
    public struct PlayerCharacterInputs
    {
        public float MoveAxisForward;    // 前后移动轴
        public float MoveAxisRight;      // 左右移动轴
        public Quaternion CameraRotation; // 摄像机旋转
        public bool JumpDown;            // 跳跃按下
        public bool CrouchDown;          // 蹲下按下
        public bool CrouchUp;            // 蹲下释放
        public bool SprintDown;          // 冲刺按下
    }

    /// <summary>
    /// AI角色输入数据结构
    /// </summary>
    public struct AICharacterInputs
    {
        public Vector3 MoveVector;  // 移动向量
        public Vector3 LookVector;  // 朝向向量
    }

    /// <summary>
    /// 额外朝向方法枚举
    /// </summary>
    public enum BonusOrientationMethod
    {
        None,                       // 无额外朝向
        TowardsGravity,             // 朝向重力
        TowardsGroundSlopeAndGravity, // 朝向地面坡度和重力
    }

    /// <summary>
    /// 动画配置
    /// </summary>
    [System.Serializable]
    public class AnimationConfig
    {
        public AnimationState State;
        public ClipTransition Transition;
        public float FadeDuration = 0.25f;
        public float Speed = 1f;
        public bool Loop = true;
    }

    /// <summary>
    /// 角色控制器主类 - 集成Animancer和KCC的完整版本
    /// </summary>
    public class MyTestController3 : MonoBehaviour, ICharacterController
    {
        #region 核心组件
        [Header("核心组件")]
        public KinematicCharacterMotor Motor;  // 运动控制器核心组件
        [SerializeField] private AnimancerComponent _Animancer;  // 动画组件
        #endregion

        #region 移动系统参数
        [Header("地面移动参数")]
        public float MaxStableMoveSpeed = 10f;        // 最大稳定移动速度
        public float StableMovementSharpness = 15f;   // 地面移动响应速度
        public float OrientationSharpness = 10f;      // 角色朝向响应速度
        public OrientationMethod OrientationMethod = OrientationMethod.TowardsCamera;  // 朝向方法选择

        [Header("空中移动参数")]
        public float MaxAirMoveSpeed = 15f;           // 最大空中移动速度
        public float AirAccelerationSpeed = 15f;      // 空中加速度
        public float Drag = 0.1f;                     // 空气阻力系数
        #endregion

        #region 跳跃系统参数
        [Header("跳跃系统参数")]
        public bool AllowJumpingWhenSliding = false;  // 是否允许在滑行时跳跃
        public float JumpUpSpeed = 10f;               // 跳跃上升速度
        public float JumpScalableForwardSpeed = 10f;  // 跳跃时前向速度缩放
        public float jumpForwordWeight = 0.1f;
        public float JumpPreGroundingGraceTime = 0f;  // 跳跃前接地宽限时间
        public float JumpPostGroundingGraceTime = 0f; // 跳跃后接地宽限时间
        #endregion

        #region 物理和碰撞参数
        [Header("物理和碰撞参数")]
        public List<Collider> IgnoredColliders = new List<Collider>();  // 忽略的碰撞体列表
        public BonusOrientationMethod BonusOrientationMethod = BonusOrientationMethod.None;  // 额外朝向方法
        public float BonusOrientationSharpness = 10f;                   // 额外朝向响应速度
        public Vector3 Gravity = new Vector3(0, -30f, 0);               // 重力向量
        #endregion

        #region 变换和引用
        [Header("变换和引用")]
        public Transform MeshRoot;                    // 模型根节点
        public Transform CameraFollowPoint;           // 摄像机跟随点
        public float CrouchedCapsuleHeight = 1f;      // 蹲下时的胶囊体高度
        #endregion

        #region 动画系统
        [Header("动画系统")]
        [SerializeField] private AnimationConfig[] _AnimationConfigs;  // 动画配置数组
        [SerializeField] private float _AnimationTransitionTime = 0.25f;  // 动画切换时间
        [SerializeField] private bool _EnableAnimationDebug = true;  // 启用动画调试
        
        // 动画状态管理
        private AnimationState _CurrentAnimationState = AnimationState.Idle;
        private AnimationState _PreviousAnimationState = AnimationState.Idle;
        private Dictionary<AnimationState, ClipTransition> _AnimationCache;
        private AnimancerState _CurrentAnimancerState;
        
        // 动画事件
        public System.Action<AnimationState, AnimationState> OnAnimationStateChanged;
        public System.Action<AnimationState> OnAnimationStarted;
        public System.Action<AnimationState> OnAnimationCompleted;
        #endregion

        #region 状态管理
        [field: Header("角色状态")]
        public CharacterState CurrentCharacterState { get; private set; }  // 当前角色状态
        public bool CanSetState = false;
        
        // 状态检查属性
        public bool IsGrounded => Motor.GroundingStatus.IsStableOnGround;
        public bool IsMoving => _moveInputVector.sqrMagnitude > 0.1f;
        public bool IsJumping => CurrentCharacterState == CharacterState.Jumping;
        public bool IsFalling => CurrentCharacterState == CharacterState.Falling;
        public bool IsSwimming => CurrentCharacterState == CharacterState.Swimming;
        public bool IsCrouching => _isCrouching;
        public bool IsStunned => CurrentCharacterState == CharacterState.Stunned;
        public bool IsInVehicle => CurrentCharacterState == CharacterState.InVehicle;
        public float CurrentMoveSpeed => Motor.Velocity.magnitude;
        public Vector3 MoveInputVector => _moveInputVector;
        #endregion

        #region 私有变量
        // 物理和碰撞状态
        private Collider[] _probedColliders = new Collider[8];   // 探测的碰撞体数组
        private RaycastHit[] _probedHits = new RaycastHit[8];    // 探测的射线命中数组
        private Vector3 lastInnerNormal = Vector3.zero;         // 上次内部法线
        private Vector3 lastOuterNormal = Vector3.zero;         // 上次外部法线

        // 移动和旋转状态
        private Vector3 _moveInputVector;  // 处理后的移动输入向量
        private Vector3 _lookInputVector;  // 处理后的朝向输入向量
        private Vector3 _lastMovementDirection;  // 上次移动方向

        // 跳跃系统状态
        private bool _jumpRequested = false;                    // 跳跃请求标志
        private bool _jumpConsumed = false;                     // 跳跃已消耗标志
        private bool _jumpedThisFrame = false;                  // 本帧是否跳跃
        private bool _isJumpingThisFrame = false;               // 本帧是否正在跳跃（用于避免速度叠加）
        private float _timeSinceJumpRequested = Mathf.Infinity; // 距离跳跃请求的时间
        private float _timeSinceLastAbleToJump = 0f;            // 距离上次能跳跃的时间

        // 物理和碰撞状态
        private Vector3 _internalVelocityAdd = Vector3.zero;    // 内部速度添加（用于外力）

        // 蹲下系统状态
        private bool _shouldBeCrouching = false;  // 是否应该蹲下
        private bool _isCrouching = false;        // 当前是否蹲下
        
        // 冲刺系统状态
        private bool _isSprinting = false;        // 当前是否冲刺
        #endregion

        #region Unity生命周期
        /// <summary>
        /// 初始化 - 设置组件引用和初始状态
        /// </summary>
        private void Awake()
        {
            // 处理初始状态
            TransitionToState(CharacterState.Default);

            // 将角色控制器分配给运动控制器
            Motor.CharacterController = this;
            
            // 初始化动画系统
            InitializeAnimationSystem();
        }

        private void Update()
        {
            // 更新动画状态
            UpdateAnimationState();
        }
        #endregion

        #region 动画系统
        /// <summary>
        /// 初始化动画系统
        /// </summary>
        private void InitializeAnimationSystem()
        {
            if (_Animancer == null)
            {
                _Animancer = GetComponent<AnimancerComponent>();
                if (_Animancer == null)
                {
                    Debug.LogError("[MyTestController3] 未找到AnimancerComponent组件！");
                    return;
                }
            }

            // 初始化动画缓存
            _AnimationCache = new Dictionary<AnimationState, ClipTransition>();
            
            // 缓存动画配置
            if (_AnimationConfigs != null)
            {
                foreach (var config in _AnimationConfigs)
                {
                    if (config.Transition != null)
                    {
                        _AnimationCache[config.State] = config.Transition;
                    }
                }
            }

            // 播放初始动画
            PlayAnimation(AnimationState.Idle);
            
            Debug.Log("[MyTestController3] 动画系统初始化完成");
        }

        /// <summary>
        /// 更新动画状态
        /// </summary>
        private void UpdateAnimationState()
        {
            AnimationState targetAnimationState = DetermineTargetAnimationState();
            
            if (targetAnimationState != _CurrentAnimationState)
            {
                TransitionToAnimationState(targetAnimationState);
            }
        }

        /// <summary>
        /// 确定目标动画状态
        /// </summary>
        private AnimationState DetermineTargetAnimationState()
        {
            // 特殊状态检查（优先级最高）
            if (IsStunned) return AnimationState.Idle;
            if (IsInVehicle) return AnimationState.Vehicle_Idle;
            if (IsSwimming) return DetermineSwimmingAnimationState();
            
            // 空中状态
            if (!IsGrounded)
            {
                if (IsJumping) return AnimationState.Jump_Loop;
                if (IsFalling) return AnimationState.Fall_Loop;
                return AnimationState.Fall_Loop;
            }
            
            // 着陆状态
            if (CurrentCharacterState == CharacterState.Landing)
            {
                return AnimationState.Jump_Land;
            }
            
            // 蹲下状态
            if (IsCrouching)
            {
                if (IsMoving)
                {
                    return DetermineCrouchMovementAnimationState();
                }
                return AnimationState.Crouch_Idle;
            }
            
            // 移动状态
            if (IsMoving)
            {
                return DetermineMovementAnimationState();
            }
            
            // 停止状态
            if (IsStopping())
            {
                return DetermineStopAnimationState();
            }
            
            return AnimationState.Idle;
        }

        /// <summary>
        /// 确定移动动画状态
        /// </summary>
        private AnimationState DetermineMovementAnimationState()
        {
            // 计算8方向
            Vector2 direction2D = new Vector2(_moveInputVector.x, _moveInputVector.z).normalized;
            string directionSuffix = GetDirectionSuffix(direction2D);
            
            // 根据速度确定移动类型
            string movementType = "";
            float speedRatio = CurrentMoveSpeed / MaxStableMoveSpeed;
            
            if (_isSprinting && speedRatio > 0.8f)
            {
                movementType = "Sprint";
            }
            else if (speedRatio > 0.6f)
            {
                movementType = "Run";
            }
            else
            {
                movementType = "Walk";
            }
            
            // 组合动画状态名
            string stateName = $"{movementType}_{directionSuffix}";
            
            // 尝试解析为枚举
            if (System.Enum.TryParse<AnimationState>(stateName, out AnimationState result))
            {
                return result;
            }
            
            // 如果找不到精确匹配，返回默认方向
            string defaultStateName = $"{movementType}_Forward";
            if (System.Enum.TryParse<AnimationState>(defaultStateName, out AnimationState defaultResult))
            {
                return defaultResult;
            }
            
            return AnimationState.Idle;
        }

        /// <summary>
        /// 确定游泳动画状态
        /// </summary>
        private AnimationState DetermineSwimmingAnimationState()
        {
            if (!IsMoving)
            {
                return AnimationState.Swim_Idle;
            }
            
            Vector2 direction2D = new Vector2(_moveInputVector.x, _moveInputVector.z).normalized;
            string directionSuffix = GetDirectionSuffix(direction2D);
            
            string stateName = $"Swim_{directionSuffix}";
            
            if (System.Enum.TryParse<AnimationState>(stateName, out AnimationState result))
            {
                return result;
            }
            
            return AnimationState.Swim_Forward;
        }

        /// <summary>
        /// 确定蹲下移动动画状态
        /// </summary>
        private AnimationState DetermineCrouchMovementAnimationState()
        {
            Vector2 direction2D = new Vector2(_moveInputVector.x, _moveInputVector.z).normalized;
            
            if (direction2D.y > 0.5f)
            {
                return AnimationState.Crouch_Walk_Forward;
            }
            else if (direction2D.y < -0.5f)
            {
                return AnimationState.Crouch_Walk_Backward;
            }
            
            return AnimationState.Crouch_Idle;
        }

        /// <summary>
        /// 确定停止动画状态
        /// </summary>
        private AnimationState DetermineStopAnimationState()
        {
            if (_lastMovementDirection.z > 0.5f)
            {
                return AnimationState.Stop_Forward;
            }
            else if (_lastMovementDirection.z < -0.5f)
            {
                return AnimationState.Stop_Backward;
            }
            
            return AnimationState.Idle;
        }

        /// <summary>
        /// 获取方向后缀
        /// </summary>
        private string GetDirectionSuffix(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360;
            
            // 8方向划分
            if (angle >= 337.5f || angle < 22.5f) return "Right";
            if (angle >= 22.5f && angle < 67.5f) return "ForwardRight";
            if (angle >= 67.5f && angle < 112.5f) return "Forward";
            if (angle >= 112.5f && angle < 157.5f) return "ForwardLeft";
            if (angle >= 157.5f && angle < 202.5f) return "Left";
            if (angle >= 202.5f && angle < 247.5f) return "BackwardLeft";
            if (angle >= 247.5f && angle < 292.5f) return "Backward";
            if (angle >= 292.5f && angle < 337.5f) return "BackwardRight";
            
            return "Forward";
        }

        /// <summary>
        /// 切换到动画状态
        /// </summary>
        private void TransitionToAnimationState(AnimationState newState)
        {
            if (_CurrentAnimationState == newState) return;
            
            _PreviousAnimationState = _CurrentAnimationState;
            _CurrentAnimationState = newState;
            
            // 播放动画
            PlayAnimation(newState);
            
            // 触发事件
            OnAnimationStateChanged?.Invoke(_PreviousAnimationState, _CurrentAnimationState);
            
            if (_EnableAnimationDebug)
            {
                Debug.Log($"[动画] {_PreviousAnimationState} -> {_CurrentAnimationState}");
            }
        }

        /// <summary>
        /// 播放动画
        /// </summary>
        public void PlayAnimation(AnimationState state)
        {
            if (_AnimationCache.TryGetValue(state, out var transition))
            {
                _CurrentAnimancerState = _Animancer.Play(transition, _AnimationTransitionTime);
                
                // 设置动画事件
                if (_CurrentAnimancerState != null)
                {
                    //_CurrentAnimancerState.Events.OnEnd = () => OnAnimationCompleted?.Invoke(state);
                }
                
                OnAnimationStarted?.Invoke(state);
            }
            else
            {
                Debug.LogWarning($"[动画] 未找到动画状态: {state}");
            }
        }

        /// <summary>
        /// 检查是否正在停止
        /// </summary>
        private bool IsStopping()
        {
            return !IsMoving && _lastMovementDirection.sqrMagnitude > 0.1f && CurrentMoveSpeed > 0.1f;
        }
        #endregion

        #region 状态管理系统
        /// <summary>
        /// 处理移动状态转换和进入/退出回调
        /// </summary>
        /// <param name="newState">新状态</param>
        public void TransitionToState(CharacterState newState)
        {
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        /// <summary>
        /// 进入状态时的事件
        /// </summary>
        /// <param name="state">进入的状态</param>
        /// <param name="fromState">来自的状态</param>
        public void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    break;
                case CharacterState.Jumping:
                    PlayAnimation(AnimationState.Jump_Start);
                    break;
                case CharacterState.Falling:
                    PlayAnimation(AnimationState.Fall_Start);
                    break;
                case CharacterState.Landing:
                    PlayAnimation(AnimationState.Jump_Land);
                    break;
            }
        }

        /// <summary>
        /// 退出状态时的事件
        /// </summary>
        /// <param name="state">退出的状态</param>
        /// <param name="toState">前往的状态</param>
        public void OnStateExit(CharacterState state, CharacterState toState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    break;
            }
        }

        /// <summary>
        /// 状态更新方法
        /// </summary>
        public void UpdateCharacterState()
        {
            if (!CanSetState)
            {
                // 自动状态更新逻辑
                UpdateAutomaticState();
            }
        }

        /// <summary>
        /// 自动状态更新
        /// </summary>
        private void UpdateAutomaticState()
        {
            // 根据当前状态和输入确定目标状态
            CharacterState targetState = DetermineTargetState();
            
            if (targetState != CurrentCharacterState)
            {
                TransitionToState(targetState);
            }
        }

        /// <summary>
        /// 确定目标状态
        /// </summary>
        private CharacterState DetermineTargetState()
        {
            // 特殊状态检查
            if (IsStunned) return CharacterState.Stunned;
            if (IsInVehicle) return CharacterState.InVehicle;
            if (IsSwimming) return CharacterState.Swimming;
            
            // 空中状态
            if (!IsGrounded)
            {
                if (IsJumping) return CharacterState.Jumping;
                return CharacterState.Falling;
            }
            
            // 着陆状态
            if (CurrentCharacterState == CharacterState.Falling && IsGrounded)
            {
                return CharacterState.Landing;
            }
            
            // 蹲下状态
            if (IsCrouching) return CharacterState.Crouching;
            
            // 移动状态
            if (IsMoving)
            {
                if (_isSprinting) return CharacterState.Running;
                return CharacterState.Walking;
            }
            
            return CharacterState.Idle;
        }
        #endregion

        #region 输入处理系统
        /// <summary>
        /// 设置玩家输入
        /// </summary>
        /// <param name="inputs">玩家角色输入</param>
        public void SetInputs(ref PlayerCharacterInputs inputs)
        {
            // 限制输入
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

            // 计算角色平面上的摄像机方向和旋转
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Idle:
                case CharacterState.Walking:
                case CharacterState.Running:
                    {
                        // 移动和朝向输入
                        _moveInputVector = cameraPlanarRotation * moveInputVector;
                        
                        // 更新移动方向
                        if (_moveInputVector.sqrMagnitude > 0.1f)
                        {
                            _lastMovementDirection = _moveInputVector.normalized;
                        }

                        switch (OrientationMethod)
                        {
                            case OrientationMethod.TowardsCamera:
                                _lookInputVector = cameraPlanarDirection;
                                break;
                            case OrientationMethod.TowardsMovement:
                                _lookInputVector = _moveInputVector.normalized;
                                break;
                        }

                        // 跳跃输入
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // 冲刺输入
                        _isSprinting = inputs.SprintDown && IsMoving;

                        // 蹲下输入
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                                if (MeshRoot != null)
                                {
                                    MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                                }
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }

                        break;
                    }
            }
        }

        /// <summary>
        /// 设置AI输入
        /// </summary>
        /// <param name="inputs">AI角色输入</param>
        public void SetInputs(ref AICharacterInputs inputs)
        {
            _moveInputVector = inputs.MoveVector;
            _lookInputVector = inputs.LookVector;
        }
        #endregion

        #region 角色更新系统
        /// <summary>
        /// 临时旋转变量
        /// </summary>
        private Quaternion _tmpTransientRot;

        /// <summary>
        /// 在角色开始移动更新之前调用
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// 更新角色旋转
        /// </summary>
        /// <param name="currentRotation">当前旋转</param>
        /// <param name="deltaTime">时间增量</param>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Idle:
                case CharacterState.Walking:
                case CharacterState.Running:
                    {
                        if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
                        {
                            // 平滑地从当前朝向插值到目标朝向
                            Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                            // 设置当前旋转
                            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                        }

                        Vector3 currentUp = (currentRotation * Vector3.up);
                        if (BonusOrientationMethod == BonusOrientationMethod.TowardsGravity)
                        {
                            // 从当前向上方向旋转到重力反方向
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        else if (BonusOrientationMethod == BonusOrientationMethod.TowardsGroundSlopeAndGravity)
                        {
                            if (Motor.GroundingStatus.IsStableOnGround)
                            {
                                Vector3 initialCharacterBottomHemiCenter = Motor.TransientPosition + (currentUp * Motor.Capsule.radius);

                                Vector3 smoothedGroundNormal = Vector3.Slerp(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGroundNormal) * currentRotation;

                                // 移动位置以创建围绕底部半球中心的旋转
                                Motor.SetTransientPosition(initialCharacterBottomHemiCenter + (currentRotation * Vector3.down * Motor.Capsule.radius));
                            }
                            else
                            {
                                Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                            }
                        }
                        else
                        {
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, Vector3.up, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// 更新角色速度
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Idle:
                case CharacterState.Walking:
                case CharacterState.Running:
                    {
                        // 地面移动
                        if (Motor.GroundingStatus.IsStableOnGround)
                        {
                            float currentVelocityMagnitude = currentVelocity.magnitude;

                            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

                            // 在斜坡上重新定向速度
                            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

                            // 计算目标速度
                            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                            
                            // 根据冲刺状态调整速度
                            float targetSpeed = _isSprinting ? MaxStableMoveSpeed * 1.5f : MaxStableMoveSpeed;
                            Vector3 targetMovementVelocity = reorientedInput * targetSpeed;

                            // 平滑移动速度
                            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
                        }
                        // 空中移动
                        else
                        {
                            // 修复：在跳跃时跳过空中移动速度计算，避免速度叠加
                            if (!_isJumpingThisFrame && _moveInputVector.sqrMagnitude > 0f)
                            {
                                Vector3 addedVelocity = _moveInputVector * AirAccelerationSpeed * deltaTime;

                                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                                // 限制来自输入的空中速度
                                if (currentVelocityOnInputsPlane.magnitude < MaxAirMoveSpeed)
                                {
                                    // 限制添加的速度，使总速度不超过输入平面上的最大速度
                                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, MaxAirMoveSpeed);
                                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                                }
                                else
                                {
                                    // 确保添加的速度不会与已经超速的速度同向
                                    if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                                    {
                                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                                    }
                                }

                                // 防止空中攀爬斜坡墙壁
                                if (Motor.GroundingStatus.FoundAnyGround)
                                {
                                    if (Vector3.Dot(currentVelocity + addedVelocity, addedVelocity) > 0f)
                                    {
                                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, perpenticularObstructionNormal);
                                    }
                                }

                                // 应用添加的速度
                                currentVelocity += addedVelocity;
                            }

                            // 重力
                            currentVelocity += Gravity * deltaTime;

                            // 阻力
                            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
                        }

                        // 处理跳跃
                        HandleJumping(ref currentVelocity, deltaTime);

                        // 考虑附加速度
                        if (_internalVelocityAdd.sqrMagnitude > 0f)
                        {
                            currentVelocity += _internalVelocityAdd;
                            _internalVelocityAdd = Vector3.zero;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// 处理跳跃逻辑
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        private void HandleJumping(ref Vector3 currentVelocity, float deltaTime)
        {
            _isJumpingThisFrame = false;
            _timeSinceJumpRequested += deltaTime;
            
            if (_jumpRequested)
            {
                // 检查我们是否真的被允许跳跃
                if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                {
                    // 在离开地面之前计算跳跃方向
                    Vector3 jumpDirection = Motor.CharacterUp;
                    if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                    {
                        jumpDirection = Motor.GroundingStatus.GroundNormal;
                    }

                    // 使角色在下一次更新时跳过地面探测/吸附
                    Motor.ForceUnground();

                    // 记录跳跃前速度用于调试
                    Vector3 beforeJumpVelocity = currentVelocity;

                    // 添加到返回速度并重置跳跃状态
                    currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                    
                    // 修复：只在跳跃时添加一次前向速度，避免与空中移动速度叠加
                    if (_moveInputVector.sqrMagnitude > 0f)
                    {
                        Vector3 jumpForwardVelocity = _moveInputVector * JumpScalableForwardSpeed * jumpForwordWeight;
                        currentVelocity += jumpForwardVelocity;
                    }
                    
                    _jumpRequested = false;
                    _jumpConsumed = true;
                    _jumpedThisFrame = true;
                    _isJumpingThisFrame = true;
                    
                    // 切换到跳跃状态
                    TransitionToState(CharacterState.Jumping);
                }
            }
        }

        /// <summary>
        /// 在角色完成移动更新后调用
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Idle:
                case CharacterState.Walking:
                case CharacterState.Running:
                    {
                        // 处理跳跃相关值
                        {
                            // 处理跳跃前接地宽限时间
                            if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                            {
                                _jumpRequested = false;
                            }

                            if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                            {
                                // 如果我们在地面上，重置跳跃值
                                if (!_jumpedThisFrame)
                                {
                                    _jumpConsumed = false;
                                }
                                _timeSinceLastAbleToJump = 0f;
                            }
                            else
                            {
                                // 跟踪距离上次能够跳跃的时间（用于宽限时间）
                                _timeSinceLastAbleToJump += deltaTime;
                            }
                        }

                        // 处理站起
                        if (_isCrouching && !_shouldBeCrouching)
                        {
                            // 对角色站立高度进行重叠测试，看是否有障碍物
                            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                            if (Motor.CharacterOverlap(
                                Motor.TransientPosition,
                                Motor.TransientRotation,
                                _probedColliders,
                                Motor.CollidableLayers,
                                QueryTriggerInteraction.Ignore) > 0)
                            {
                                // 如果有障碍物，保持蹲下尺寸
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                            }
                            else
                            {
                                // 如果没有障碍物，站起
                                if (MeshRoot != null)
                                {
                                    MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                                }
                                _isCrouching = false;
                            }
                        }
                        break;
                    }
            }
        }
        #endregion

        #region 碰撞和物理系统
        /// <summary>
        /// 接地后更新 - 处理着陆和离开地面
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        public void PostGroundingUpdate(float deltaTime)
        {
            // 处理着陆和离开地面
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLanded();
            }
            else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLeaveStableGround();
            }
        }

        /// <summary>
        /// 检查碰撞体是否对碰撞有效
        /// </summary>
        /// <param name="coll">要检查的碰撞体</param>
        /// <returns>是否有效</returns>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (IgnoredColliders.Count == 0)
            {
                return true;
            }

            if (IgnoredColliders.Contains(coll))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 地面碰撞回调
        /// </summary>
        /// <param name="hitCollider">碰撞的碰撞体</param>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="hitPoint">碰撞点</param>
        /// <param name="hitStabilityReport">碰撞稳定性报告</param>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 移动碰撞回调
        /// </summary>
        /// <param name="hitCollider">碰撞的碰撞体</param>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="hitPoint">碰撞点</param>
        /// <param name="hitStabilityReport">碰撞稳定性报告</param>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 添加速度（用于外力影响）
        /// </summary>
        /// <param name="velocity">要添加的速度</param>
        public void AddVelocity(Vector3 velocity)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Idle:
                case CharacterState.Walking:
                case CharacterState.Running:
                    {
                        _internalVelocityAdd += velocity;
                        break;
                    }
            }
        }

        /// <summary>
        /// 处理碰撞稳定性报告
        /// </summary>
        /// <param name="hitCollider">碰撞的碰撞体</param>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="hitPoint">碰撞点</param>
        /// <param name="atCharacterPosition">角色位置</param>
        /// <param name="atCharacterRotation">角色旋转</param>
        /// <param name="hitStabilityReport">碰撞稳定性报告</param>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 着陆事件
        /// </summary>
        protected void OnLanded()
        {
            if (CurrentCharacterState == CharacterState.Jumping || CurrentCharacterState == CharacterState.Falling)
            {
                TransitionToState(CharacterState.Landing);
            }
        }

        /// <summary>
        /// 离开稳定地面事件
        /// </summary>
        protected void OnLeaveStableGround()
        {
            if (CurrentCharacterState != CharacterState.Jumping)
            {
                TransitionToState(CharacterState.Falling);
            }
        }

        /// <summary>
        /// 离散碰撞检测回调
        /// </summary>
        /// <param name="hitCollider">碰撞的碰撞体</param>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
        #endregion

        #region 公共接口
        /// <summary>
        /// 强制播放动画
        /// </summary>
        /// <param name="animationState">动画状态</param>
        public void ForcePlayAnimation(AnimationState animationState)
        {
            PlayAnimation(animationState);
        }

        /// <summary>
        /// 设置冲刺状态
        /// </summary>
        /// <param name="sprinting">是否冲刺</param>
        public void SetSprinting(bool sprinting)
        {
            _isSprinting = sprinting;
        }

        /// <summary>
        /// 获取当前动画状态
        /// </summary>
        /// <returns>当前动画状态</returns>
        public AnimationState GetCurrentAnimationState()
        {
            return _CurrentAnimationState;
        }

        /// <summary>
        /// 获取动画组件
        /// </summary>
        /// <returns>Animancer组件</returns>
        public AnimancerComponent GetAnimancer()
        {
            return _Animancer;
        }
        #endregion
    }
} 