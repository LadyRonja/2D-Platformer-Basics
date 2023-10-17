using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Developed by Ronja Mattsson
/// git: https://github.com/LadyRonja
/// linkedin: https://www.linkedin.com/in/ronja-mattsson-922943205/
/// itch.io: https://ladyronja.itch.io/
/// </summary>

/// TODO:
/// Additional parameters: zoom, padding
/// Lookahead
/// Missing states
/// Additional states
///     ShootTo - replace current directional shake, allowing directional overshake

public enum CameraStates
{
    Inactive,
    TrackingSingle,
    TrackingMultiple,
    MoveToTarget,
    Shaking
}

public class CameraController : MonoBehaviour
{
    [Header("Generic")]
    public static CameraController Instance;
    private Camera cam;
    public CameraStates state = CameraStates.TrackingSingle;
    [SerializeField] float debugShakeAmount = 2f;
    [SerializeField] float debugFreezeAmount = 0.1f;

    [Header("Single object Tracking")]
    public Transform objectToFollow;
    Vector3 positionToCenter;
    public bool ignoreX = false;
    public bool ignoreY = false;

    [Header("Screen Shake")]
    [SerializeField] bool additiveShake = true;
    [SerializeField] bool prioritizeLargeShake = true;
    float shakeLeft = 0f;
    [SerializeField] float shakeStabalizer = 5f;
    Vector2 shakeDirection = Vector2.zero;
    CameraStates previousState = CameraStates.TrackingSingle;

    private void Awake()
    {
        #region Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
        #endregion

        cam = Camera.main;
    }

    private void Update()
    {
        if (Time.timeScale == 0) return;

        #region Debug
        if (Input.GetKeyDown(KeyCode.K))
        {
            ShakeScreen(debugShakeAmount);
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButtonDown((int)MouseButton.Left))
        {
            Vector3 direction = Camera.main.ScreenToWorldPoint(Input.mousePosition) - objectToFollow.position;
            ShakeScreen(debugShakeAmount, direction.normalized);
            FreezeGame(debugFreezeAmount);
        }
        #endregion

        CameraBehaivour(state);
    }

    private void CameraBehaivour(CameraStates operationState)
    {
        switch (operationState)
        {
            case CameraStates.Inactive:
                // Do nothing
                break;
            case CameraStates.TrackingSingle:
                TrackSingleTarget();
                break;
            case CameraStates.TrackingMultiple:
                Debug.Log("Tracking Multiple not implemented yet, setting state to track single");
                state = CameraStates.TrackingSingle;
                break;
            case CameraStates.MoveToTarget:
                Debug.Log("MoveToTarget not implemented yet, setting state to track single");
                state = CameraStates.TrackingSingle;
                break;
            case CameraStates.Shaking:
                Shaking();
                break;
            default:
                Debug.LogError("Reached end of switch-state-machine, states not covered?");
                Debug.Log("Switching to Inactive cameaState");
                state = CameraStates.Inactive;
                break;
        }
    }

    private void TrackSingleTarget()
    {
        if(!ignoreX) positionToCenter.x = objectToFollow.position.x;
        if(!ignoreY) positionToCenter.y = objectToFollow.position.y;

        cam.transform.position = new Vector3(positionToCenter.x, positionToCenter.y, cam.transform.position.z);
    }

    private void Shaking()
    {
        // Long shakes should not keep camera in place
        CameraBehaivour(previousState);

        // Get a random direction and amount of shake
        Vector2 direction = Random.insideUnitCircle.normalized;
        float amount = Random.Range(-shakeLeft, shakeLeft);

        // If shake should not be random, fix direction and don't let the amount go behind the direction
        if (shakeDirection != Vector2.zero)
        {
            direction = shakeDirection.normalized;
            amount = Mathf.Abs(amount);
        }

        // Update position
        cam.transform.position += (Vector3)direction * amount;
       
        // Reduce amount of shake left
        shakeLeft -= shakeStabalizer * Time.deltaTime;

        // If done shaking, return to the previous camera behaivor
        if (shakeLeft <= 0f)
        {
            state = previousState;
            shakeDirection = Vector2.zero;
        }
    }

    public void ShakeScreen(float amount)
    {
        if (additiveShake) shakeLeft += amount;
        else if (prioritizeLargeShake) shakeLeft = Mathf.Max(shakeLeft, amount);
        else shakeLeft = amount;

        if (state != CameraStates.Shaking) previousState = state;
        state = CameraStates.Shaking;
    }

    public void ShakeScreen(float amount, Vector2 direction)
    {
        shakeDirection = direction;
        ShakeScreen(amount);
    }

    public void FreezeGame(float duration)
    {
        StartCoroutine(Freeze(duration));
    }

    private IEnumerator Freeze(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}