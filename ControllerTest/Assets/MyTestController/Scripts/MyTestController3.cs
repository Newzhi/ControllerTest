using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using Animancer;
using System;
using UnityEngine.InputSystem;

namespace MyTestController.Scripts
{  /// <summary>
    /// 角色状态枚举
    /// </summary>
    public enum CharacterState
    {
        Default,
        Idle,
        Walking,
        Running,
        Jumping,
        Falling,
        Swimming,
        Crouching,
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
    /// 动画配置类
    /// </summary>
    [System.Serializable]
    public class AnimationConfig
    {
        public CharacterState State;
        public ClipTransition Transition;
        public float FadeDuration = 0.25f;
        public float Speed = 1f;
        public bool Loop = true;
    }

    /// <summary>
    /// 角色控制器主类 - 实现基于KinematicCharacterController的角色移动系统
    /// </summary>
    public class MyTestController3 : MonoBehaviour, ICharacterController
    {
        #region 核心组件
        [Header("核心组件")]
        public KinematicCharacterMotor Motor;  // 运动控制器核心组件
        public AnimancerComponent Animancer;
        #endregion

        #region 移动系统参数
        [Header("地面移动参数")]
        public float MaxStableMoveSpeed = 10f;        // 最大稳定移动速度
        public float MaxRunningSpeed = 15f;           // 最大跑步速度
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
        public float CrouchedCapsuleHeight = 1f;      // 蹲下时的胶囊体高度
        #endregion

        #region 变换和引用
        [Header("变换和引用")]
        public Transform MeshRoot;                    // 模型根节点
        public Transform CameraFollowPoint;           // 摄像机跟随点
        #endregion
        
        #region 动画系统
        [Header("动画配置")] 
        public AnimationConfig[] AnimationConfigs;
        private Dictionary<CharacterState, ClipTransition> _AnimationCache;
        private CharacterState _CurrentAnimationState = CharacterState.Idle;
        #endregion

        #region 状态管理
        [Header("角色状态")]
        public bool CanSetState = false;
        public CharacterState CurrentCharacterState { get; private set; }  // 当前角色状态
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

        // 跳跃系统状态
        private bool _jumpRequested = false;                    // 跳跃请求标志
        private bool _jumpConsumed = false;                     // 跳跃已消耗标志
        private bool _jumpedThisFrame = false;                  // 本帧是否跳跃
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
            // 初始化动画系统
            InitializeAnimationSystem();

            // 处理初始状态
            TransitionToState(CharacterState.Idle);

            // 将角色控制器分配给运动控制器
            Motor.CharacterController = this;
        }

        private void Update()
        {
            UpdateCharacterState();    // 更新角色状态
            UpdateAnimationState();    // 更新动画状态
        }
        
        #endregion

        #region 动画系统
        /// <summary>
        /// 初始化动画系统
        /// </summary>
        private void InitializeAnimationSystem()
        {
            if (Animancer == null)
            {
                Animancer = GetComponent<AnimancerComponent>();
                if (Animancer == null)
                {
                    Debug.LogError("[MyTestController2] 未找到AnimancerComponent组件！");
                    return;
                }
            }

            // 初始化动画缓存
            _AnimationCache = new Dictionary<CharacterState, ClipTransition>();
            
            // 缓存动画配置
            if (AnimationConfigs != null)
            {
                foreach (var config in AnimationConfigs)
                {
                    if (config.Transition != null)
                    {
                        _AnimationCache[config.State] = config.Transition;
                    }
                }
            }

            // 播放初始动画
            PlayAnimation(CharacterState.Idle);
            
            Debug.Log("[MyTestController2] 动画系统初始化完成");
        }

        /// <summary>
        /// 更新动画状态
        /// </summary>
        private void UpdateAnimationState()
        {
            CharacterState targetAnimationState = DetermineTargetAnimationState();
            
            if (targetAnimationState != _CurrentAnimationState)
            {
                TransitionToAnimationState(targetAnimationState);
            }
        }

        /// <summary>
        /// 确定目标动画状态
        /// </summary>
        private CharacterState DetermineTargetAnimationState()
        {
            // 根据当前角色状态和物理状态确定动画
            if (!Motor.GroundingStatus.IsStableOnGround)
            {
                if (Motor.Velocity.y > 0)
                    return CharacterState.Jumping;
                else
                    return CharacterState.Falling;
            }

            if (_isCrouching)
                return CharacterState.Crouching;

            float moveMagnitude = _moveInputVector.magnitude;
            if (moveMagnitude > 0.1f)
            {
                return _isSprinting ? CharacterState.Running : CharacterState.Walking;
            }

            return CharacterState.Idle;
        }

        /// <summary>
        /// 转换到动画状态
        /// </summary>
        private void TransitionToAnimationState(CharacterState newState)
        {
            if (_AnimationCache.TryGetValue(newState, out var transition))
            {
                Animancer.Play(transition);
                _CurrentAnimationState = newState;
            }
        }

        /// <summary>
        /// 播放指定动画
        /// </summary>
        private void PlayAnimation(CharacterState state)
        {
            if (_AnimationCache.TryGetValue(state, out var transition))
            {
                Animancer.Play(transition);
                _CurrentAnimationState = state;
            }
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
                case CharacterState.Idle:
                    Debug.Log("进入空闲状态");
                    break;
                case CharacterState.Walking:
                    Debug.Log("进入行走状态");
                    break;
                case CharacterState.Running:
                    Debug.Log("进入跑步状态");
                    break;
                case CharacterState.Jumping:
                    Debug.Log("进入跳跃状态");
                    break;
                case CharacterState.Falling:
                    Debug.Log("进入下落状态");
                    break;
                case CharacterState.Crouching:
                    Debug.Log("进入蹲下状态");
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
                case CharacterState.Idle:
                    Debug.Log("退出空闲状态");
                    break;
                case CharacterState.Walking:
                    Debug.Log("退出行走状态");
                    break;
                case CharacterState.Running:
                    Debug.Log("退出跑步状态");
                    break;
                case CharacterState.Jumping:
                    Debug.Log("退出跳跃状态");
                    break;
                case CharacterState.Falling:
                    Debug.Log("退出下落状态");
                    break;
                case CharacterState.Crouching:
                    Debug.Log("退出蹲下状态");
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
                // 根据物理状态和输入自动更新状态
                if (!Motor.GroundingStatus.IsStableOnGround)
                {
                    if (Motor.Velocity.y > 0)
                        TransitionToState(CharacterState.Jumping);
                    else
                        TransitionToState(CharacterState.Falling);
                }
                else if (_isCrouching)
                {
                    TransitionToState(CharacterState.Crouching);
                }
                else
                {
                    float moveMagnitude = _moveInputVector.magnitude;
                    if (moveMagnitude > 0.1f)
                    {
                        TransitionToState(_isSprinting ? CharacterState.Running : CharacterState.Walking);
                    }
                    else
                    {
                        TransitionToState(CharacterState.Idle);
                    }
                }
            }
        }
        #endregion

        #region 输入处理系统

        /// <summary>
        /// 每帧由制定的Player中间脚本调用，告诉角色其输入是什么
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
                case CharacterState.Idle:
                case CharacterState.Walking:
                case CharacterState.Running:
                case CharacterState.Crouching:
                    {
                        // 移动和朝向输入
                        _moveInputVector = cameraPlanarRotation * moveInputVector;

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

                        // 蹲下输入
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                                //MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }

                        // 冲刺输入
                        _isSprinting = inputs.SprintDown && _moveInputVector.magnitude > 0.1f && !_isCrouching;

                        break;
                    }
                case CharacterState.Jumping:
                case CharacterState.Falling:
                    {
                        // 空中移动和朝向输入
                        _moveInputVector = cameraPlanarRotation * moveInputVector;
                        
                        // 更新朝向输入，允许空中控制旋转
                        switch (OrientationMethod)
                        {
                            case OrientationMethod.TowardsCamera:
                                _lookInputVector = cameraPlanarDirection;
                                break;
                            case OrientationMethod.TowardsMovement:
                                _lookInputVector = _moveInputVector.normalized;
                                break;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// 每帧由AI脚本调用，告诉角色其输入是什么
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
        /// （由KinematicCharacterMotor在其更新周期中调用）
        /// 在角色开始移动更新之前调用
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            
        }

        /// <summary>
        /// （由KinematicCharacterMotor在其更新周期中调用）
        /// 在这里告诉角色其旋转应该是什么。这是设置角色旋转的唯一地方
        /// </summary>
        /// <param name="currentRotation">当前旋转</param>
        /// <param name="deltaTime">时间增量</param>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // 处理朝向输入（地面和空中都支持）
            if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
            {
                // 平滑地从当前朝向插值到目标朝向
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                // 设置当前旋转（将被KinematicCharacterMotor使用）
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }

            // 处理额外朝向方法
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

                    // 移动位置以创建围绕底部半球中心的旋转，而不是围绕轴心
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
        }

        /// <summary>
        /// （由KinematicCharacterMotor在其更新周期中调用）
        /// 在这里告诉角色其速度应该是什么。这是设置角色速度的唯一地方
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
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
                
                // 根据状态选择最大速度
                float maxSpeed = _isSprinting ? MaxRunningSpeed : MaxStableMoveSpeed;
                if (_isCrouching) maxSpeed *= 0.5f; // 蹲下时速度减半
                
                Vector3 targetMovementVelocity = reorientedInput * maxSpeed;

                // 平滑移动速度
                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
            }
            // 空中移动
            else
            {
                // 添加移动输入
                if (_moveInputVector.sqrMagnitude > 0f)
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
            _jumpedThisFrame = false;
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

                    // 使角色在下一次更新时跳过地面探测/吸附。
                    Motor.ForceUnground();

                    // 添加到返回速度并重置跳跃状态
                    currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                    currentVelocity += (_moveInputVector * JumpScalableForwardSpeed * jumpForwordWeight);
                    _jumpRequested = false;
                    _jumpConsumed = true;
                    _jumpedThisFrame = true;
                }
            }

            // 考虑附加速度
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        /// <summary>
        /// （由KinematicCharacterMotor在其更新周期中调用）
        /// 在角色完成移动更新后调用
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        public void AfterCharacterUpdate(float deltaTime)
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
                    //MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                    _isCrouching = false;
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
            _internalVelocityAdd += velocity;
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
            Debug.Log("角色着陆");
        }

        /// <summary>
        /// 离开稳定地面事件
        /// </summary>
        protected void OnLeaveStableGround()
        {
            Debug.Log("角色离开地面");
        }

        /// <summary>
        /// 离散碰撞检测回调
        /// </summary>
        /// <param name="hitCollider">碰撞的碰撞体</param>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
        #endregion
    }
} 