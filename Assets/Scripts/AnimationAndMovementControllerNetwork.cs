using System;
using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnimationAndMovementControllerNetwork : NetworkBehaviour
{
    [SerializeField] CinemachineCamera FLCam;
    [SerializeField] AudioListener listener;
    [SerializeField] Camera cinemachineBrainCamera;
    [SerializeField] AudioSource movementAudioSource;
    
    [SerializeField] AudioClip[] walkingSounds;
    [SerializeField] AudioClip[] runningSounds;

    bool playFirstWalkingSound = true;  
    bool playFirstRunningSound = true;
    CharacterInputs characterInputs;
    Vector2 currentMovementInput;
    Vector3 currentMovement;
    Vector3 currentRunMovement;
    Vector3 _cameraRelativeMovement;
    [SerializeField] float rotationFactorPerFrame = 15.0f;
    [SerializeField]public float movementSpeed = 5f;
    //[SerializeField] GameObject throwablePrefab;
    bool isMovementPressed;
    bool isRunPressed;
    CharacterController characterController;
    Animator animator;
    bool isJumpPressed = false;
    //bool isJumping = false;
    bool isJumpAnimating = false;
    bool isAttacking = false;
    bool isFalling = false;
    // bool isAttackAnimating = false;
    // bool isAttackPressed = false;
    //float initialJumpVelocity;
    [SerializeField] float maxJumpTime = 2f;
    [SerializeField] float maxJumpHeight = 40.0f;
    [SerializeField] float AttackDuration = 0.6f;
    [SerializeField] float fallRecoveryTime = 4f;
    float groundedGravity = -0.05f;
    int isWalkingHash;
    int isRunningHash;
    int isJumpingHash;
    int isFallingHash;
    //int isAttackingHash;
    float gravity = -9.8f;
    Netcode_Functions netcode_functions;
    Coroutine walkingSoundCoroutine;
    Coroutine runningSoundCoroutine;

    public override void OnNetworkSpawn()
    {
        characterInputs = FindAnyObjectByType<EnableInputSystem>().GetComponent<EnableInputSystem>().characterInputs;

        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();



        isWalkingHash = Animator.StringToHash("isWalking");
        isRunningHash = Animator.StringToHash("isRunning");
        isFallingHash = Animator.StringToHash("isFalling");
        //isJumpingHash = Animator.StringToHash("isJumping");
        //isAttackingHash = Animator.StringToHash("isAttacking");

        characterInputs.CharacterControls.Move.started += onMovementInput;
        characterInputs.CharacterControls.Move.canceled += onMovementInput;
        characterInputs.CharacterControls.Move.performed += onMovementInput;
        characterInputs.CharacterControls.Run.started += onRun;
        characterInputs.CharacterControls.Run.canceled += onRun;
        //characterInputs.CharacterControls.Jump.started += onJump;
        //characterInputs.CharacterControls.Jump.canceled += onJump;
        //characterInputs.CharacterControls.Attack.started += OnAttack;
        //characterInputs.CharacterControls.Attack.canceled += OnAttack;


        //setUpJumpVariables();
        if (IsOwner)
        {
            FLCam.Priority = 1;
            listener.enabled = true;
            
            StartCoroutine(RegisterNameAfterSpawn());
        }
        else
        {
            FLCam.Priority = 0;
        }

        base.OnNetworkSpawn();
    }
    // [ServerRpc]
    // void RegisterAuthIdServerRpc(string authId, ServerRpcParams rpcParams = default)
    // {
    //     Netcode_Functions.Instance.RegisterClientAuthId(
    //         rpcParams.Receive.SenderClientId, authId);
    // }
    //private void OnAttack(InputAction.CallbackContext context)
    //{
    //    isAttackPressed = context.ReadValueAsButton();
    //    //Debug.Log("Attack pressed/released");
    //}

    //void setUpJumpVariables()
    //{
    //    float timeToApex = maxJumpTime / 2;
    //    gravity = (-2 * maxJumpHeight) / Mathf.Pow(timeToApex, 2);
    //    initialJumpVelocity = (2 * maxJumpHeight) / timeToApex;
    //}
    //private void onJump(InputAction.CallbackContext context)
    //{
    //    isJumpPressed = context.ReadValueAsButton();
    //}
    IEnumerator RegisterNameAfterSpawn()
    {
        // Wait one frame so all NetworkObjects finish spawning
        yield return null;

        string playerName = PlayerPrefs.GetString("PlayerName", $"Player {OwnerClientId}");
        Netcode_Functions netFunctions = FindAnyObjectByType<Netcode_Functions>();
        if (netFunctions != null && netFunctions.IsSpawned)
        {
            netFunctions.RegisterPlayerNameServerRpc(playerName, AuthenticationService.Instance.PlayerId);
        }
        else
        {
            Debug.LogWarning("[AnimationController] Netcode_Functions not spawned yet when registering name");
        }
    }

    public void RevivePlayerMovements()
    {
        StartCoroutine(RevivePlayerMovementsCoroutine());
    }
    IEnumerator RevivePlayerMovementsCoroutine()
    {   
        //animator.ResetTrigger("Die");
        //animator.SetTrigger("Revive");
        animator.Rebind();
        animator.Update(0f);
        animator.Update(0f);
        yield return new WaitForSeconds(1.0f);
       // animator.ResetTrigger("Revive");
    }
    private void onRun(InputAction.CallbackContext context)
    {
        isRunPressed = context.ReadValueAsButton();
    }


    void onMovementInput(InputAction.CallbackContext context)
    {
        currentMovementInput = context.ReadValue<Vector2>();
        currentMovement.x = currentMovementInput.x;
        currentMovement.z = currentMovementInput.y;

        currentRunMovement.x = currentMovementInput.x * 3.0f;
        currentRunMovement.y = 0f;
        currentRunMovement.z = currentMovementInput.y * 3.0f;

        isMovementPressed = currentMovementInput.x != 0 || currentMovementInput.y != 0;
    }

    void handleGravity()
    {
        bool isFalling = currentMovement.y <= 0 || !isJumpPressed;
        float fallMultiplier = 2f;
        if (characterController.isGrounded)
        {
            if (isJumpAnimating)
            {
                animator.SetBool(isJumpingHash, false);
                isJumpAnimating = false;
            }
            currentMovement.y = groundedGravity;
            currentRunMovement.y = groundedGravity;
        }
        else if (isFalling)
        {
            float previousYVelocity = currentMovement.y;
            float newYVelocity = currentMovement.y + (gravity * Time.deltaTime);
            float nextYVelocity = (previousYVelocity + newYVelocity) * 0.5f;
            currentMovement.y = nextYVelocity;
            currentRunMovement.y = nextYVelocity;
        }
        else
        {
            float previousYVelocity = currentMovement.y;
            float newYVelocity = currentMovement.y + (gravity * fallMultiplier * Time.deltaTime);
            float nextYVelocity = (previousYVelocity + newYVelocity) * 0.5f;
            currentMovement.y = nextYVelocity;
            currentRunMovement.y = nextYVelocity;
        }
    }

    void handleAnimation()
    {
        bool isWalking = animator.GetBool(isWalkingHash);
        bool isRunning = animator.GetBool(isRunningHash);

        if (!isWalking && isMovementPressed)
        {
            animator.SetBool(isWalkingHash, true);
            PlayOrPauseWalkingSoundServerRpc(true);
            // Mathf.Lerp(animator.GetFloat("Velocity"), 0.05f, 0.01f);
            //animator.SetFloat("Velocity", 0.05f);
        }
        else if (!isMovementPressed && isWalking)
        {
            animator.SetBool(isWalkingHash, false);
            PlayOrPauseWalkingSoundServerRpc(false);
            //Mathf.Lerp(animator.GetFloat("Velocity"), 0f, 0.01f);
            //animator.SetFloat("Velocity", 0.0f);
        }
        if ((isMovementPressed && isRunPressed) && !isRunning)
        {
            animator.SetBool(isRunningHash, true);
            PlayOrPauseRunningSoundServerRpc(true);
            //Mathf.Lerp(animator.GetFloat("Velocity"), 0.3f, 0.05f);
            //animator.SetFloat("Velocity", 0.3f);
        }
        else if ((!isMovementPressed || !isRunPressed) && isRunning)
        {
            animator.SetBool(isRunningHash, false);
            PlayOrPauseRunningSoundServerRpc(false);
            //Mathf.Lerp(animator.GetFloat("Velocity"), 0.05f, 0.05f);
            // if (isMovementPressed)
            // {
            //     animator.SetFloat("Velocity", 0.05f);
            // }
            // else
            // {
            //     animator.SetFloat("Velocity", 0f);
            //  }
        }

    }

    void handleRotation()
    {
        Vector3 positionToLookAt;
        positionToLookAt.x = _cameraRelativeMovement.x;
        positionToLookAt.y = 0f;
        positionToLookAt.z = _cameraRelativeMovement.z;

        if (isMovementPressed)
        {
            Quaternion currentRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationFactorPerFrame * Time.deltaTime);

        }
    }

    [ServerRpc]
    void PlayOrPauseRunningSoundServerRpc(bool isRunning)
    {
        PlayOrPauseRunningSoundClientRpc(isRunning);  
    }

    [ClientRpc]
    void PlayOrPauseRunningSoundClientRpc(bool isRunning)
    {   
        if(isRunning)
        {   
            if(runningSoundCoroutine != null)
            {
                StopCoroutine(runningSoundCoroutine);
            }
            runningSoundCoroutine = StartCoroutine(PlayRunningSoundCoroutine());
            if(walkingSoundCoroutine != null)
            {
                StopCoroutine(walkingSoundCoroutine);
                walkingSoundCoroutine = null;
            }
           
        }
        else
        {   
            StopCoroutine(runningSoundCoroutine);
            runningSoundCoroutine = null;
        }
       
    }
    IEnumerator PlayRunningSoundCoroutine()
    {
        while(true)
        {
            movementAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            if (playFirstRunningSound)
            {
                movementAudioSource.PlayOneShot(runningSounds[0]);
            }
            else
            {
                movementAudioSource.PlayOneShot(runningSounds[1]);
            }
            playFirstRunningSound = !playFirstRunningSound; // Toggle for next iteration
            
            
            yield return new WaitForSeconds(0.3F);
        }

    }

    [ServerRpc]
    void PlayOrPauseWalkingSoundServerRpc(bool isWalking)
    {
        PlayOrPauseWalkingSoundClientRpc(isWalking);
    }

    [ClientRpc]
    void PlayOrPauseWalkingSoundClientRpc(bool isWalking)
    {
        if (isWalking)
        {
            if (walkingSoundCoroutine != null)
            {
                StopCoroutine(walkingSoundCoroutine);
            }
            walkingSoundCoroutine = StartCoroutine(PlayWalkingSoundCoroutine());
            if (runningSoundCoroutine != null)
            {
                StopCoroutine(runningSoundCoroutine);
                runningSoundCoroutine = null;
            }
            
        }
        else
        {
            StopCoroutine(walkingSoundCoroutine);
            walkingSoundCoroutine = null;
        }

    }
    IEnumerator PlayWalkingSoundCoroutine()
    {
        while (true)
        {
            while (true)
            {
                movementAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                if (playFirstWalkingSound)
                {
                    movementAudioSource.PlayOneShot(walkingSounds[0]);
                }
                else
                {
                    movementAudioSource.PlayOneShot(walkingSounds[1]);
                }
                playFirstWalkingSound = !playFirstWalkingSound; // Toggle for next iteration


                yield return new WaitForSeconds(0.55F);
            }
        }
    }
        //void handleJump()
        //{
        //    if (!isJumping && isJumpPressed && characterController.isGrounded)
        //    {
        //        isJumping = true;
        //        animator.SetBool(isJumpingHash, true);
        //        isJumpAnimating = true;
        //        currentMovement.y = initialJumpVelocity * 0.5f;
        //        currentRunMovement.y = initialJumpVelocity * 0.5f;
        //    }
        //    else if (isJumping && !isJumpPressed && characterController.isGrounded)
        //    {
        //        isJumping = false;
        //    }
        //}

        //void handleAttack()
        //{
        //    if (!isAttacking && !isAttackAnimating && isAttackPressed && !isRunPressed && !isJumping)
        //    {
        //        isAttacking = true;
        //        animator.SetBool(isAttackingHash, true);
        //        isAttackAnimating = true;
        //        //Debug.Log("Attack started");
        //        StartCoroutine(StopAttackAfterAttackDurationCoroutine());

        //    }
        //    else if (!isAttacking && isAttackAnimating && !isAttackPressed)
        //    {
        //        animator.SetBool(isAttackingHash, false);
        //        isAttackAnimating = false;
        //        //Debug.Log("Attack stopped");
        //    }
        //}

        //IEnumerator StopAttackAfterAttackDurationCoroutine()
        //{
        //    yield return new WaitForSeconds(AttackDuration);
        //    isAttacking = false;

        //    //Debug.Log("Attack duration ended");
        //}

        //[ServerRpc]
        //void ThrowObstacleServerRpc(ServerRpcParams rpcParams = default)
        //{
        //    GameObject spawnedThrowable = Instantiate(throwablePrefab, transform);
        //    var netObj = spawnedThrowable.GetComponent<NetworkObject>();

        //    if (netObj == null)
        //    {
        //        Debug.Log("Network object component missing on the throwable prefab");
        //    }
        //    netObj.Spawn();
        //}

        //  Update is called once per frame
        void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        handleAnimation();
        

        if (!isAttacking && !isFalling)
        {
            if (isRunPressed)
            {
                _cameraRelativeMovement = ConvertToCameraSpace(currentRunMovement);

                characterController.Move(_cameraRelativeMovement * Time.deltaTime * movementSpeed);
            }
            else
            {
                _cameraRelativeMovement = ConvertToCameraSpace(currentMovement);
                characterController.Move(_cameraRelativeMovement * Time.deltaTime * movementSpeed);
            }


            handleGravity();
            //handleJump();
            //handleAttack();

            //if (Input.GetKeyDown(KeyCode.F))
            //{
            //    ThrowObstacleServerRpc();
            //}
        }
        handleRotation();

    }

    Vector3 ConvertToCameraSpace(Vector3 vectorToRotate)
    {   
        float currentYValue = vectorToRotate.y; 
        Vector3 cameraForward = cinemachineBrainCamera.transform.forward;
        Vector3 cameraRight = cinemachineBrainCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward = cameraForward.normalized;
        cameraRight = cameraRight.normalized;

        Vector3 cameraForwardZProduct = vectorToRotate.z * cameraForward;
        Vector3 cameraRightXProduct = vectorToRotate.x * cameraRight;

        Vector3 vectorRotatedToCameraSpace = cameraForwardZProduct + cameraRightXProduct;
        vectorRotatedToCameraSpace.y = currentYValue; // Preserve the original Y value
        return vectorRotatedToCameraSpace;
    }
    void CheckRole()
    {

    }

    public void TakeAFall()
    {
        isFalling = true;
        animator.SetBool(isFallingHash, true);
        StartCoroutine(fallCoroutine());
    }
    IEnumerator fallCoroutine()
    {
        
        yield return new WaitForSeconds(fallRecoveryTime);
        isFalling = false;
        animator.SetBool(isFallingHash, false);

    }

    public void PassFLCamDataToVisuals(DangerVisuals visuals,GameObject throwableMagic)
    {
        visuals.AssignSwitcherObjectsWithOwnedPole(transform, FLCam, throwableMagic,transform);
    }
    
}
