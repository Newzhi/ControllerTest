using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class TPC_Learning : MonoBehaviour
{
    Transform playerTransform;
    Animator animator;
    Transform cameraTransform;
    CharacterController characterController;
    
     #region 玩家姿态及相关动画参数阈值
    public enum PlayerPosture
    {
        Crouch,
        Stand,
        Falling,
        Jumping,
        Landing,
        Climbing,
        Push
    };
    //[HideInInspector]
    public PlayerPosture playerPosture = PlayerPosture.Stand;

    float crouchThreshold = 0f;
    float standThreshold = 1f;
    float midairThreshold = 2.1f;
    float landingThreshold = 1f;
    #endregion

    //玩家运动状态
    public enum LocomotionState
    {
        Idle,
        Walk,
        Run
    };
    [HideInInspector]
    public LocomotionState locomotionState = LocomotionState.Idle;


    //玩家装备状态
    public enum ArmState
    {
        Normal,
        Aim
    };
    [HideInInspector]
    public ArmState armState = ArmState.Normal;

    //玩家不同状态的运动速度
    float crouchSpeed = 1.5f;
    float walkSpeed = 2.5f;
    float runSpeed = 5.5f;

    #region 输入值
    Vector2 moveInput;
    bool isRunPressed;
    bool isCrouchPressed;
    bool isAimPressed;
    bool isJumpPressed;
    bool isPushPressed;
    #endregion

    #region 状态机参数的哈希值
    int postureHash;
    int moveSpeedHash;
    int turnSpeedHash;
    int verticalVelHash;
    int feetTweenHash;
    #endregion

    Vector3 playerMovementWorldSpace = Vector3.zero;
    Vector3 playerMovement = Vector3.zero;

    //重力
    public float gravity = -9.8f;

    //垂直方向速度
    float VerticalVelocity;

    //最大跳起高度
    public float maxHeight = 1.5f;

    //滞空左右脚状态
    float feetTween;

    #region 速度缓存池定义
    static readonly int CACHE_SIZE = 3;
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    int currentChacheIndex = 0;
    Vector3 averageVel = Vector3.zero;
    #endregion

    //下落时加速度的倍数
    float fallMultiplier = 1.5f;

    //玩家是否着地
    bool isGrounded;

    //玩家是否可以跌落
    bool couldFall;

    //跌落的最小高度，小于此高度不会切换到跌落姿态
    float fallHeight = 0.5f;

    //是否处于跳跃CD状态
    bool isLanding;

    //地标检测射线的偏移量
    float groundCheckOffset = 0.5f;

    //跳跃的CD设置
    float jumpCD = 0.15f;

    //上一帧的动画nornalized时间
    float lastFootCycle = 0;

    #region 翻越相关
    /*
    PlayerSensor playerSensor;

    bool isClimbReady;

    int defaultClimbParameter = 0;
    int vaultParameter = 1;
    int lowClimbParameter = 2;
    int highClimbParameter = 3;
    int currentClimbparameter;

    Vector3 leftHandPosition;
    Vector3 rightHandPosition;
    Vector3 rightFootPosition;
    */
    #endregion

    #region 推拉相关
    /*
    bool pushStateChanged;
    Transform interactPoint;
    Transform rightHandTarget;
    Transform leftHandTarget;
    MovableObject movableObject;

    public RigBuilder rigBuilder;
    public TwoBoneIKConstraint rightHandConstraint;
    public TwoBoneIKConstraint leftHandConstraint;
    */
    #endregion


    
    void Start()
    {
        
    }

    
    void Update()
    {
        
    }
}
