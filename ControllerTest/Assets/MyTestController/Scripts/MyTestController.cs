using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyNamespace
{ 
    public enum CharacterState 
    {
        Default,    // 默认状态
        Walk,       // 行走状态
        Run,        // 奔跑状态
        Crouch,     // 蹲下状态
        // 可扩展的状态：Jump, Fall, Swim, Climb, Slide 等
    }

    public enum OrientationMethod
    {
        TowardsCamera,
        TowardsMovement,
    }

    /// <summary>
    /// 玩家输入数据结构
    /// </summary>
    public struct PlayerInputs
    {
        // 移动输入
        public Vector2 moveInput;        // 移动输入向量 (x=左右, y=前后)
        
        // 摄像机输入
        public Quaternion CameraRotation; // 摄像机旋转四元数
        
        // 动作输入
        public bool JumpDown;            // 跳跃按下
        public bool isCrouch;            // 蹲下状态
        public bool RunDown;             // 奔跑按下
    }
    /// <summary>
    /// 角色控制器主类 - 实现基于KinematicCharacterController的角色移动系统
    /// </summary>
    public class MyTestController : MonoBehaviour, ICharacterController
    {
        #region 核心组件
        [Header("核心组件")]
        public KinematicCharacterMotor Motor;  // 运动控制器核心组件
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
        public float JumpPreGroundingGraceTime = 0f;  // 跳跃前接地宽限时间
        public float JumpPostGroundingGraceTime = 0f; // 跳跃后接地宽限时间
        #endregion

        #region 状态系统参数
        [Header("状态系统参数")]
        public float StateTransitionTime = 0.1f;      // 状态切换时间
        public bool EnableStateLogging = false;       // 是否启用状态日志
        #endregion

        #region 物理和碰撞参数
        [Header("物理和碰撞参数")]
        public List<Collider> IgnoredColliders = new List<Collider>();  // 忽略的碰撞体列表
        public float BonusOrientationSharpness = 10f;                   // 额外朝向响应速度
        public Vector3 Gravity = new Vector3(0, -30f, 0);               // 重力向量
        #endregion

        #region 变换和引用
        [Header("变换和引用")]
        public Transform MeshRoot;                    // 模型根节点
        public Transform CameraFollowPoint;           // 摄像机跟随点
        public float CrouchedCapsuleHeight = 1f;      // 蹲下时的胶囊体高度
        #endregion

        #region 状态管理
        //[Header("角色状态")]
        public CharacterState CurrentCharacterState { get; private set; }  // 当前角色状态
        #endregion

        #region 输入系统
        [Header("输入系统")]
        private PlayerInputs _currentInputs = new PlayerInputs();  // 当前输入数据实例
        #endregion

        #region 移动和旋转状态
        [Header("移动和旋转状态")]
        private Vector3 _moveInputVector;  // 处理后的移动输入向量
        private Vector3 _lookInputVector;  // 处理后的朝向输入向量
        #endregion

        #region 跳跃系统状态
        [Header("跳跃系统状态")]
        private bool _jumpRequested = false;                    // 跳跃请求标志
        private bool _jumpConsumed = false;                     // 跳跃已消耗标志
        private bool _jumpedThisFrame = false;                  // 本帧是否跳跃
        private float _timeSinceJumpRequested = Mathf.Infinity; // 距离跳跃请求的时间
        private float _timeSinceLastAbleToJump = 0f;            // 距离上次能跳跃的时间
        #endregion

        #region 物理和碰撞状态
        [Header("物理和碰撞状态")]
        private Collider[] _probedColliders = new Collider[8];   // 探测的碰撞体数组
        private RaycastHit[] _probedHits = new RaycastHit[8];    // 探测的射线命中数组
        private Vector3 _internalVelocityAdd = Vector3.zero;    // 内部速度添加（用于外力）
        private Vector3 lastInnerNormal = Vector3.zero;         // 上次内部法线
        private Vector3 lastOuterNormal = Vector3.zero;         // 上次外部法线
        #endregion

        #region 蹲下系统状态
        [Header("蹲下系统状态")]
        private bool _shouldBeCrouching = false;  // 是否应该蹲下
        private bool _isCrouching = false;        // 当前是否蹲下
        #endregion

        #region 状态管理系统
        [Header("状态管理系统")]
        private CharacterState _previousState = CharacterState.Default;  // 上一个状态
        private float _stateTransitionStartTime = 0f;                    // 状态切换开始时间
        private bool _isTransitioning = false;                           // 是否正在切换状态
        private Rigidbody _rigidbody;                                    // 刚体组件引用
        #endregion

        #region Unity生命周期
        /// <summary>
        /// 初始化 - 设置组件引用和初始状态
        /// </summary>
        void Awake()
        {
            // 获取KinematicCharacterMotor组件
            if (Motor == null)
            {
                Motor = GetComponent<KinematicCharacterMotor>();
            }
            
            // 获取刚体组件
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }
            
            // 设置刚体属性
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            
            // 设置角色控制器
            if (Motor != null)
            {
                Motor.CharacterController = this;
            }
        }

        /// <summary>
        /// 启动时调用 - 初始化状态
        /// </summary>
        void Start()
        {
            // 初始化角色状态
            TransitionToState(CharacterState.Default);
            
            // 初始化输入数据
            _currentInputs = new PlayerInputs();
        }
                #endregion

        #region 状态管理系统
        /// <summary>
        /// 处理输入并更新状态
        /// </summary>
        private void ProcessInputs()
        {
            // 处理移动输入
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(_currentInputs.moveInput.x, 0f, _currentInputs.moveInput.y), 1f);

            // 计算摄像机方向和旋转
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(_currentInputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(_currentInputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
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
                        if (_currentInputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // 蹲下输入
                        if (_currentInputs.isCrouch)
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
                        else
                        {
                            _shouldBeCrouching = false;
                        }

                        // 状态切换逻辑
                        UpdateCharacterState();
                        break;
                    }
            }
        }

        /// <summary>
        /// 更新角色状态
        /// </summary>
        private void UpdateCharacterState()
        {
            CharacterState newState = DetermineCharacterState();
            
            if (newState != CurrentCharacterState)
            {
                TransitionToState(newState);
            }
        }

        /// <summary>
        /// 确定当前应该的状态
        /// </summary>
        /// <returns>目标状态</returns>
        private CharacterState DetermineCharacterState()
        {
            // 基础状态判断逻辑
            if (_isCrouching)
            {
                return CharacterState.Crouch;
            }
            else if (_currentInputs.RunDown && _moveInputVector.sqrMagnitude > 0.1f)
            {
                return CharacterState.Run;
            }
            else if (_moveInputVector.sqrMagnitude > 0.1f)
            {
                return CharacterState.Walk;
            }
            
            return CharacterState.Default;
        }

        /// <summary>
        /// 状态切换处理
        /// </summary>
        /// <param name="newState">新状态</param>
        public void TransitionToState(CharacterState newState)
        {
            if (_isTransitioning) return; // 防止重复切换
            
            CharacterState oldState = CurrentCharacterState;
            
            // 退出当前状态
            OnStateExit(oldState, newState);
            
            // 更新状态
            _previousState = oldState;
            CurrentCharacterState = newState;
            _stateTransitionStartTime = Time.time;
            _isTransitioning = true;
            
            // 进入新状态
            OnStateEnter(newState, oldState);
            
            if (EnableStateLogging)
            {
                Debug.Log($"状态切换: {oldState} -> {newState}");
            }
        }

        /// <summary>
        /// 状态进入事件
        /// </summary>
        /// <param name="state">进入的状态</param>
        /// <param name="fromState">来自的状态</param>
        public void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    // 默认状态进入逻辑
                    break;
                case CharacterState.Walk:
                    // 行走状态进入逻辑
                    break;
                case CharacterState.Run:
                    // 奔跑状态进入逻辑
                    break;
                case CharacterState.Crouch:
                    // 蹲下状态进入逻辑
                    break;
            }
            
            // 这里可以添加动画事件调用
            // OnAnimationStateEnter(state);
        }

        /// <summary>
        /// 状态退出事件
        /// </summary>
        /// <param name="state">退出的状态</param>
        /// <param name="toState">前往的状态</param>
        public void OnStateExit(CharacterState state, CharacterState toState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    // 默认状态退出逻辑
                    break;
                case CharacterState.Walk:
                    // 行走状态退出逻辑
                    break;
                case CharacterState.Run:
                    // 奔跑状态退出逻辑
                    break;
                case CharacterState.Crouch:
                    // 蹲下状态退出逻辑
                    break;
            }
            
            // 这里可以添加动画事件调用
            // OnAnimationStateExit(state);
        }
        #endregion

        #region 动画系统扩展接口
        /// <summary>
        /// 动画状态进入事件（可扩展）
        /// </summary>
        /// <param name="state">进入的状态</param>
        protected virtual void OnAnimationStateEnter(CharacterState state)
        {
            // 子类可以重写此方法来实现动画控制
            // 例如：animator.SetTrigger("Enter" + state.ToString());
        }

        /// <summary>
        /// 动画状态退出事件（可扩展）
        /// </summary>
        /// <param name="state">退出的状态</param>
        protected virtual void OnAnimationStateExit(CharacterState state)
        {
            // 子类可以重写此方法来实现动画控制
            // 例如：animator.SetTrigger("Exit" + state.ToString());
        }

        /// <summary>
        /// 获取当前移动速度（用于动画）
        /// </summary>
        /// <returns>移动速度</returns>
        public float GetCurrentMoveSpeed()
        {
            return Motor.Velocity.magnitude;
        }

        /// <summary>
        /// 获取当前是否在地面上（用于动画）
        /// </summary>
        /// <returns>是否在地面上</returns>
        public bool IsGrounded()
        {
            return Motor.GroundingStatus.IsStableOnGround;
        }

        /// <summary>
        /// 获取当前是否在移动（用于动画）
        /// </summary>
        /// <returns>是否在移动</returns>
        public bool IsMoving()
        {
            return _moveInputVector.sqrMagnitude > 0.1f;
        }

        /// <summary>
        /// 获取当前是否在奔跑（用于动画）
        /// </summary>
        /// <returns>是否在奔跑</returns>
        public bool IsRunning()
        {
            return CurrentCharacterState == CharacterState.Run;
        }

        /// <summary>
        /// 获取当前是否在蹲下（用于动画）
        /// </summary>
        /// <returns>是否在蹲下</returns>
        public bool IsCrouching()
        {
            return _isCrouching;
        }
        #endregion

        #region 输入处理系统
        /// <summary>
        /// 处理移动输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetMoveInput(InputAction.CallbackContext ctx)
        {
            _currentInputs.moveInput = ctx.ReadValue<Vector2>();
        }

        /// <summary>
        /// 处理奔跑输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetRunInput(InputAction.CallbackContext ctx)
        {
            _currentInputs.RunDown = ctx.ReadValueAsButton();
        }

        /// <summary>
        /// 处理蹲下输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetCrouchInput(InputAction.CallbackContext ctx)
        {
            _currentInputs.isCrouch = ctx.ReadValueAsButton();
        }

        /// <summary>
        /// 处理跳跃输入
        /// </summary>
        /// <param name="ctx">输入上下文</param>
        public void GetJumpInput(InputAction.CallbackContext ctx)
        {
            _currentInputs.JumpDown = ctx.ReadValueAsButton();
        }
        #endregion

        //角色更新系统
        /// <summary>
        /// 更新角色旋转 - 根据朝向方法选择旋转逻辑
        /// </summary>
        /// <param name="currentRotation">当前旋转</param>
        /// <param name="deltaTime">时间增量</param>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                case CharacterState.Walk:
                case CharacterState.Run:
                case CharacterState.Crouch:
                    {
                        HandleGroundRotation(ref currentRotation, deltaTime);
                        break;
                    }
            }
        }

        /// <summary>
        /// 处理地面旋转
        /// </summary>
        /// <param name="currentRotation">当前旋转</param>
        /// <param name="deltaTime">时间增量</param>
        private void HandleGroundRotation(ref Quaternion currentRotation, float deltaTime)
        {
            Vector3 lookInputVector = Vector3.zero;
            
            if (OrientationMethod == OrientationMethod.TowardsMovement)
            {
                // 朝向方法：根据移动方向旋转角色
                lookInputVector = new Vector3(_currentInputs.moveInput.x, 0, _currentInputs.moveInput.y);
            }
            else if (OrientationMethod == OrientationMethod.TowardsCamera)
            {
                // 朝向方法：根据摄像机方向旋转角色
                lookInputVector = _currentInputs.CameraRotation * Vector3.forward;
                lookInputVector.y = 0; // 确保只在水平面旋转
            }
            
            // 如果有有效的朝向输入，进行平滑旋转
            if (lookInputVector.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookInputVector);
                currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 
                    OrientationSharpness * deltaTime);
            }
        }



        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        HandleDefaultMovement(ref currentVelocity, deltaTime);
                        break;
                    }
                case CharacterState.Walk:
                    {
                        HandleWalkMovement(ref currentVelocity, deltaTime);
                        break;
                    }
                case CharacterState.Run:
                    {
                        HandleRunMovement(ref currentVelocity, deltaTime);
                        break;
                    }
                case CharacterState.Crouch:
                    {
                        HandleCrouchMovement(ref currentVelocity, deltaTime);
                        break;
                    }
            }
        }

        #region 移动处理系统
        /// <summary>
        /// 处理默认状态移动
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        private void HandleDefaultMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            HandleGroundMovement(ref currentVelocity, deltaTime, MaxStableMoveSpeed);
            HandleJumping(ref currentVelocity, deltaTime);
        }

        /// <summary>
        /// 处理行走移动
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        private void HandleWalkMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            HandleGroundMovement(ref currentVelocity, deltaTime, MaxStableMoveSpeed);
            HandleJumping(ref currentVelocity, deltaTime);
        }

        /// <summary>
        /// 处理奔跑移动
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        private void HandleRunMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            HandleGroundMovement(ref currentVelocity, deltaTime, MaxStableMoveSpeed * 1.5f);
            HandleJumping(ref currentVelocity, deltaTime);
        }

        /// <summary>
        /// 处理蹲下移动
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        private void HandleCrouchMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            HandleGroundMovement(ref currentVelocity, deltaTime, MaxStableMoveSpeed * 0.5f);
            // 蹲下时不允许跳跃
        }

        /// <summary>
        /// 处理地面移动（通用方法）
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        /// <param name="targetSpeed">目标速度</param>
        private void HandleGroundMovement(ref Vector3 currentVelocity, float deltaTime, float targetSpeed)
        {
            // 地面移动
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                float currentVelocityMagnitude = currentVelocity.magnitude;

                Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

                // 重新定向速度到斜坡
                currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

                // 计算目标速度
                Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                Vector3 targetMovementVelocity = reorientedInput * targetSpeed;

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

                    // 限制空中速度
                    if (currentVelocityOnInputsPlane.magnitude < MaxAirMoveSpeed)
                    {
                        // 限制添加的速度，使总速度不超过最大速度
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
        }

        /// <summary>
        /// 处理跳跃
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间增量</param>
        private void HandleJumping(ref Vector3 currentVelocity, float deltaTime)
        {
            // 处理跳跃相关值
            _jumpedThisFrame = false;
            _timeSinceJumpRequested += deltaTime;
            
            if (_jumpRequested)
            {
                // 检查是否允许跳跃
                if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                {
                    // 计算跳跃方向
                    Vector3 jumpDirection = Motor.CharacterUp;
                    if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                    {
                        jumpDirection = Motor.GroundingStatus.GroundNormal;
                    }

                    // 强制角色离开地面
                    Motor.ForceUnground();

                    // 添加跳跃速度
                    currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                    currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);
                    
                    _jumpRequested = false;
                    _jumpConsumed = true;
                    _jumpedThisFrame = true;
                }
            }

            // 处理内部速度添加
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }
        #endregion

        public void BeforeCharacterUpdate(float deltaTime)
        {
            // 更新摄像机旋转
            if (Camera.main != null)
            {
                _currentInputs.CameraRotation = Camera.main.transform.rotation;
            }
            
            // 处理输入并更新状态
            ProcessInputs();
        }

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
        /// 着陆事件
        /// </summary>
        protected void OnLanded()
        {
            // 这里可以添加着陆音效、粒子效果等
            if (EnableStateLogging)
            {
                Debug.Log("角色着陆");
            }
        }

        /// <summary>
        /// 离开稳定地面事件
        /// </summary>
        protected void OnLeaveStableGround()
        {
            // 这里可以添加离开地面音效、粒子效果等
            if (EnableStateLogging)
            {
                Debug.Log("角色离开地面");
            }
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
                case CharacterState.Walk:
                case CharacterState.Run:
                case CharacterState.Crouch:
                    {
                        _internalVelocityAdd += velocity;
                        break;
                    }
            }
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        HandleDefaultAfterUpdate(deltaTime);
                        break;
                    }
            }
        }

        /// <summary>
        /// 默认状态的后更新处理
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        private void HandleDefaultAfterUpdate(float deltaTime)
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
                    // 如果在地面上，重置跳跃值
                    if (!_jumpedThisFrame)
                    {
                        _jumpConsumed = false;
                    }
                    _timeSinceLastAbleToJump = 0f;
                }
                else
                {
                    // 记录距离上次能跳跃的时间（用于宽限时间）
                    _timeSinceLastAbleToJump += deltaTime;
                }
            }

            // 处理取消蹲下
            if (_isCrouching && !_shouldBeCrouching)
            {
                // 使用角色站立高度进行重叠测试，检查是否有障碍物
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
                    // 如果没有障碍物，取消蹲下
                    if (MeshRoot != null)
                    {
                        MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                    }
                    _isCrouching = false;
                }
            }

            // 完成状态切换
            if (_isTransitioning && Time.time - _stateTransitionStartTime >= StateTransitionTime)
            {
                _isTransitioning = false;
            }
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
            
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition,
            Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            
        }
    }

}
