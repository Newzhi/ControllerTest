using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class MainPlayerController : MonoBehaviour
{
    public Transform playertransform;
    public Transform cameraTransform;
    Animator animator;
    CharacterController characterController;


    //蹲下，站立，滞空
    [HideInInspector]
    public enum PlayerPosture
    {
        Crouch,
        Stand,
        Midair
    };
    [HideInInspector]
    public PlayerPosture playerPosture = PlayerPosture.Stand;

    private float crouchThreshold = 0f;
    private float standThreshold = 1f;
    private float midairThreshold = 2.1f;

   // [HideInInspector]
    public enum LocomotionState
    {
        Idle,
        Walk,
        Run
    };
    [HideInInspector]
    public LocomotionState locomotionState = LocomotionState.Idle;

    [HideInInspector]
    public enum AreState
    {
        Normal,
        Aim
    };
    [HideInInspector]
    public AreState areState = AreState.Normal;

    float crouchSpeed = 1.5f;
    float walkSpeed = 2.5f;
    float runSpeed = 5.5f;

    Vector2 moveInput;
    bool isRunning;
    bool isCrouch;
    public bool isJunmping;
    bool isAiming;

    int postureHash;
    int moveSpeedHash;
    int turnSpeedHash;
    int verticalValHash;
    
    public  Vector3 playerMovement = Vector3.zero;

    //重力
    public float gravity = -9.8f;
    //记录角色垂直速度
    float VerticalVelocity;
    //跳跃速度
    public float JumpVelocity = 5f;
    
    //交互部分
    public float interactionRange = 1.5f; // 交互范围  
    //private IInteractable currentInteractable;  
    //bool isInteracting;
    public Transform InteractPoint;
    
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        playertransform = transform;
        animator = GetComponent<Animator>();
        cameraTransform = Camera.main.transform;
        
        postureHash = Animator.StringToHash("玩家姿态");
        moveSpeedHash = Animator.StringToHash("移动速度");
        turnSpeedHash = Animator.StringToHash("转弯速度");
        verticalValHash = Animator.StringToHash("垂直速度");
    }

    
    void Update()
    {
        CaculateGravity();
        Jump();
        CaculateInput();
        SwitchPlayerState();
        SetAnimator();
        
        //交互逻辑
        //PlayerOnInteract();
        //DetectInteractables();
    }
    
    
    #region 输入
    public void GetMoveInput(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    public void GetRunInput(InputAction.CallbackContext ctx)
    {
        isRunning = ctx.ReadValueAsButton();
    }

    public void GetCrouchInput(InputAction.CallbackContext ctx)
    {
        isCrouch = ctx.ReadValueAsButton();
    }

    public void GetJumpInput(InputAction.CallbackContext ctx)
    {
        isJunmping = ctx.ReadValueAsButton();
    }

    //交互输入
    /*
    public void GetInteractInput(InputAction.CallbackContext ctx)  
    {  
        if (ctx.performed) // 只在按钮按下时检测  
        {  
            //DetectInteractables();  
            if (currentInteractable != null)  
            {  
                currentInteractable.OnInteract(gameObject);  
            }  
        }  
    }  

    /*
         public void GetInteractInput(InputAction.CallbackContext ctx)
    {
        isInteracting = ctx.ReadValueAsButton();
    }
     */
    #endregion
    
    void SwitchPlayerState()
    {
        
        if (!characterController.isGrounded)
        {
            playerPosture = PlayerPosture.Midair;
        } 
        if (isCrouch)
        {
            playerPosture = PlayerPosture.Crouch;
        }
        else
        {
            playerPosture = PlayerPosture.Stand;
        }

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

    void CaculateGravity()
    {
        if (characterController.isGrounded)
        {
            VerticalVelocity = gravity * Time.deltaTime;
            //Debug.Log("在地上"+VerticalVelocity);
            return;
        }
        else
        {
            //Debug.Log("不在地上");
            VerticalVelocity += gravity*Time.deltaTime;
        }
    }

    void Jump()
    {
        if (characterController.isGrounded && isJunmping)
        {
            VerticalVelocity = JumpVelocity;
        }
    }
    
    void CaculateInput()
    {
        Vector3 camForwardProjection = new Vector3(cameraTransform.forward.x,0,cameraTransform.forward.z).normalized;
        playerMovement = camForwardProjection * moveInput.y + cameraTransform.right*moveInput.x;
        playerMovement = playertransform.InverseTransformVector(playerMovement);
        //playerMovement = new Vector3(moveInput.x, 0, moveInput.y);
    }

    public void SetAnimator()
    {
        if (playerPosture == PlayerPosture.Stand)
        {
            animator.SetFloat(postureHash,standThreshold,0.1f, Time.deltaTime);
            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    animator.SetFloat(moveSpeedHash,0,0.1f, Time.deltaTime);
                    break;
                case LocomotionState.Walk:
                    animator.SetFloat(moveSpeedHash,playerMovement.magnitude*walkSpeed,0.1f, Time.deltaTime);
                    break;
                case LocomotionState.Run:
                    animator.SetFloat(moveSpeedHash,playerMovement.magnitude*runSpeed,0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (playerPosture == PlayerPosture.Crouch)
        {
            animator.SetFloat(postureHash,crouchThreshold,0.1f, Time.deltaTime);
            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    animator.SetFloat(moveSpeedHash,0,0.1f, Time.deltaTime);
                    break;
                default:
                    animator.SetFloat(moveSpeedHash,playerMovement.magnitude*crouchSpeed,0.1f, Time.deltaTime);
                    break;
            }
        }

        else if (playerPosture == PlayerPosture.Midair)
        {
            animator.SetFloat(postureHash,midairThreshold,0.1f, Time.deltaTime);
            animator.SetFloat(verticalValHash,VerticalVelocity,0.1f, Time.deltaTime);
        }
       
        if (true) //这里可以变换普通状态和战斗状态
        {
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
            animator.SetFloat(turnSpeedHash,rad,0.1f, Time.deltaTime);
            playertransform.Rotate(0,rad*200*Time.deltaTime,0f);
        }
    }

    public  void OnAnimatorMove()
    {
        Vector3 playerDeltaMovement = animator.deltaPosition;
        playerDeltaMovement.y = VerticalVelocity*Time.deltaTime;
        characterController.Move(playerDeltaMovement);
        //Debug.Log(playerDeltaMovement);
    }
    
    
    //交互逻辑
    /// <summary>
    /// 
    /// </summary>
    /*
    void PlayerOnInteract()
    {
        if (isInteracting && currentInteractable != null)
        {
            currentInteractable.OnInteract(gameObject); 
        }
    }
    */
    /*
    void DetectInteractables()  
    {  
        // 获取范围内的所有碰撞体  
        Collider[] hitColliders = Physics.OverlapSphere(InteractPoint.position, interactionRange);  

        IInteractable closestInteractable = null;  
        float closestDistance = float.MaxValue;  

        // 遍历所有碰撞体，找到最近的IInteractable对象  
        foreach (var hitCollider in hitColliders)  
        {  
            IInteractable interactable = hitCollider.GetComponent<IInteractable>();  
            if (interactable != null)  
            {  
                float distance = Vector3.Distance(InteractPoint.position, hitCollider.transform.position);  
                if (distance < closestDistance)  
                {  
                    closestDistance = distance;  
                    closestInteractable = interactable;  
                }  
            }  
        }  

        // 如果当前交互对象发生变化  
        if (closestInteractable != currentInteractable)  
        {  
            // 退出之前的交互范围  
            if (currentInteractable != null)  
            {  
                currentInteractable.OnExitInteractionRange(); 
                Debug.Log($"[{currentInteractable}] 离开交互范围。");
               // Debug.Log("Exiting interaction range");
            }  

            // 进入新的交互范围  
            if (closestInteractable != null)  
            {  
                closestInteractable.OnEnterInteractionRange();  
            }  

            // 更新当前交互对象  
            currentInteractable = closestInteractable;  
        }  
    }  
    */
    // 可视化交互范围（调试用）  
    private void OnDrawGizmosSelected()  
    {  
        Gizmos.color = Color.yellow;  
        Gizmos.DrawWireSphere(InteractPoint.position, interactionRange);  
    }   
}
