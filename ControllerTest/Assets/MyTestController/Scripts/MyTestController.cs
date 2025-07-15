using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.InputSystem;

public class MyTestController : MonoBehaviour, ICharacterController
{
    [Header("角色设置")]
    public float moveSpeed = 5f;
    public float jumpPower = 10f;
    public float gravity = -20f;
    
    [Header("组件引用")]
    public PlayerInput playerInput;
    
    // 私有变量
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private bool _jumpRequested;
    private bool _jumpConsumed;
    private bool _jumpedThisFrame;
    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump = 0f;
    private Vector3 _internalVelocityAdds = Vector3.zero;
    
    // 输入相关
    private InputAction _moveAction;
    private InputAction _jumpAction;
    
    void Start()
    {
        // 获取输入动作
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }
        
        if (playerInput != null)
        {
            _moveAction = playerInput.actions["Player Move"];
            _jumpAction = playerInput.actions["Jump"];
            
            // 订阅输入事件
            _jumpAction.performed += OnJump;
        }
    }
    
    void Update()
    {
        // 处理输入
        HandleInput();
    }
    
    void HandleInput()
    {
        // 获取移动输入
        if (_moveAction != null)
        {
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            _moveInputVector = new Vector3(moveInput.x, 0, moveInput.y);
        }
        
        // 处理跳跃输入
        if (_jumpRequested)
        {
            _timeSinceJumpRequested += Time.deltaTime;
        }
    }
    
    void OnJump(InputAction.CallbackContext context)
    {
        _jumpRequested = true;
        _timeSinceJumpRequested = 0f;
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // 如果有移动输入，旋转角色面向移动方向
        if (_moveInputVector.sqrMagnitude > 0f)
        {
            Vector3 lookDirection = _moveInputVector.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 10f * deltaTime);
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // 计算移动速度
        Vector3 targetMovementVelocity = _moveInputVector * moveSpeed;
        
        // 应用重力
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            // 在地面上时，重置垂直速度
            currentVelocity = targetMovementVelocity;
            
            // 处理跳跃
            if (_jumpRequested && _timeSinceJumpRequested <= 0.1f && !_jumpConsumed)
            {
                currentVelocity.y = jumpPower;
                _jumpConsumed = true;
                _jumpedThisFrame = true;
            }
        }
        else
        {
            // 在空中时，保持水平速度并应用重力
            currentVelocity.x = targetMovementVelocity.x;
            currentVelocity.z = targetMovementVelocity.z;
            currentVelocity.y += gravity * deltaTime;
        }
        
        // 应用额外的速度
        if (_internalVelocityAdds.sqrMagnitude > 0f)
        {
            currentVelocity += _internalVelocityAdds;
            _internalVelocityAdds = Vector3.zero;
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        // 重置跳跃状态
        _jumpedThisFrame = false;
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        // 着地后重置跳跃状态
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            _jumpConsumed = false;
            _timeSinceLastAbleToJump = 0f;
        }
        else
        {
            _timeSinceLastAbleToJump += deltaTime;
        }
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        // 清理跳跃请求
        if (_jumpRequested && _timeSinceJumpRequested > 0.1f)
        {
            _jumpRequested = false;
        }
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        // 默认允许所有碰撞
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        // 可以在这里处理着地逻辑
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        // 可以在这里处理移动碰撞逻辑
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
        // 处理碰撞稳定性报告
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
        // 处理离散碰撞检测
    }
    
    // 获取KinematicCharacterMotor组件
    public KinematicCharacterMotor Motor
    {
        get
        {
            if (_motor == null)
            {
                _motor = GetComponent<KinematicCharacterMotor>();
            }
            return _motor;
        }
    }
    private KinematicCharacterMotor _motor;
}
