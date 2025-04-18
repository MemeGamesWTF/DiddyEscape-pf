using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum GameState
{
    START, INPUT, GROWING, NONE, PAUSED
}

public class GameManager : MonoBehaviour
{
    [SerializeField]
    private Vector3 startPos;

    [SerializeField]
    private Vector2 minMaxRange, spawnRange;

    [SerializeField]
    private GameObject pillarPrefab, playerPrefab, stickPrefab, currentCamera;

    [SerializeField]
    private Transform rotateTransform, endRotateTransform;

    [SerializeField]
    private GameObject scorePanel, startPanel, endPanel, pausePanel;

    [SerializeField]
    private TMP_Text scoreText, scoreEndText, diamondsText, highScoreText;

    private GameObject currentPillar, nextPillar, currentStick, player;

    private int score, diamonds, highScore;

    private float cameraOffsetX;

    private GameState currentState;

    [SerializeField]
    private float stickIncreaseSpeed, maxStickSize;

    // Camera shake parameters
    [SerializeField]
    private float shakeDuration = 0.5f;
    [SerializeField]
    private float shakeMagnitude = 0.2f;

    public static GameManager instance;

    // Reference to the player's Animator
    private Animator playerAnimator;

    // Audio components
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip fallSound, tapSound, bridgeSound;

    private bool hasTapped; // To ensure tap sound plays only once per tap

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        currentState = GameState.START;

        endPanel.SetActive(false);
        scorePanel.SetActive(false);
        startPanel.SetActive(true);
        pausePanel.SetActive(false);

        score = 0;
        diamonds = PlayerPrefs.HasKey("Diamonds") ? PlayerPrefs.GetInt("Diamonds") : 0;
        highScore = PlayerPrefs.HasKey("HighScore") ? PlayerPrefs.GetInt("HighScore") : 0;

        scoreText.text = score.ToString();
        diamondsText.text = diamonds.ToString();
        highScoreText.text = highScore.ToString();

        CreateStartObjects();
        cameraOffsetX = currentCamera.transform.position.x - player.transform.position.x;

        // Get the Animator component from the player
        playerAnimator = player.GetComponentInChildren<Animator>();

        // Get the AudioSource component
        audioSource = GetComponent<AudioSource>();

        if (StateManager.instance.hasSceneStarted)
        {
            GameStart();
        }
    }

    private void Update()
    {
        if (currentState == GameState.INPUT)
        {
            // Player is idle during INPUT state
            SetPlayerAnimation(false);
            if (Input.GetMouseButtonDown(0)) // Detect initial tap
            {
                hasTapped = true;
                PlaySound(tapSound); // Play tap sound once
                currentState = GameState.GROWING;
                ScaleStick();
            }
        }

        if (currentState == GameState.GROWING)
        {
            // Player is idle during GROWING state
            SetPlayerAnimation(false);
            if (Input.GetMouseButton(0))
            {
                ScaleStick();
            }
            else
            {
                hasTapped = false; // Reset tap state for next tap
                StartCoroutine(FallStick());
            }
        }

        // Toggle pause with 'P' key or pause button
        if (Input.GetKeyDown(KeyCode.P) && (currentState == GameState.INPUT || currentState == GameState.GROWING))
        {
            TogglePause();
        }
    }

    void ScaleStick()
    {
        Vector3 tempScale = currentStick.transform.localScale;
        tempScale.y += Time.deltaTime * stickIncreaseSpeed;
        if (tempScale.y > maxStickSize)
            tempScale.y = maxStickSize;
        currentStick.transform.localScale = tempScale;
    }

    IEnumerator FallStick()
    {
        currentState = GameState.NONE;
        PlaySound(bridgeSound); // Play bridge sound when stick starts falling
        var x = Rotate(currentStick.transform, rotateTransform, 0.4f);
        yield return x;

        Vector3 movePosition = currentStick.transform.position + new Vector3(currentStick.transform.localScale.y, 0, 0);
        movePosition.y = player.transform.position.y;
        // Player is moving
        SetPlayerAnimation(true);
        x = Move(player.transform, movePosition, 0.6f);
        yield return x;
        // Player stops moving
        SetPlayerAnimation(false);

        var results = Physics2D.RaycastAll(player.transform.position, Vector2.down);
        var result = Physics2D.Raycast(player.transform.position, Vector2.down);
        foreach (var temp in results)
        {
            if (temp.collider.CompareTag("Platform"))
            {
                result = temp;
            }
        }

        if (!result || !result.collider.CompareTag("Platform"))
        {
            PlaySound(fallSound); // Play fall sound when player falls
            player.GetComponent<Rigidbody2D>().gravityScale = 1f;
            StartCoroutine(ShakeCamera()); // Trigger camera shake
            x = Rotate(currentStick.transform, endRotateTransform, 0.5f);
            yield return x;
            GameOver();
        }
        else
        {
            UpdateScore();

            movePosition = player.transform.position;
            movePosition.x = nextPillar.transform.position.x + nextPillar.transform.localScale.x * 0.5f - 0.35f;
            // Player is moving
            SetPlayerAnimation(true);
            x = Move(player.transform, movePosition, 0.6f);
            yield return x;
            // Player stops moving
            SetPlayerAnimation(false);

            movePosition = currentCamera.transform.position;
            movePosition.x = player.transform.position.x + cameraOffsetX;
            x = Move(currentCamera.transform, movePosition, 0.6f);
            yield return x;

            CreatePlatform();
            SetRandomSize(nextPillar);
            currentState = GameState.INPUT;
            Vector3 stickPosition = currentPillar.transform.position;
            stickPosition.x += currentPillar.transform.localScale.x * 0.5f - 0.05f;
            stickPosition.y = currentStick.transform.position.y;
            stickPosition.z = currentStick.transform.position.z;
            currentStick = Instantiate(stickPrefab, stickPosition, Quaternion.identity);
        }
    }

    // Camera shake coroutine
    IEnumerator ShakeCamera()
    {
        Vector3 originalPos = currentCamera.transform.position;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            currentCamera.transform.position = new Vector3(
                originalPos.x + x,
                originalPos.y + y,
                originalPos.z
            );

            elapsed += Time.deltaTime;
            yield return null;
        }

        currentCamera.transform.position = originalPos;
    }

    void CreateStartObjects()
    {
        CreatePlatform();

        Vector3 playerPos = playerPrefab.transform.position;
        playerPos.x += (currentPillar.transform.localScale.x * 0.5f - 0.35f);
        player = Instantiate(playerPrefab, playerPos, Quaternion.identity);
        player.name = "Player";

        Vector3 stickPos = stickPrefab.transform.position;
        stickPos.x += (currentPillar.transform.localScale.x * 0.5f - 0.05f);
        currentStick = Instantiate(stickPrefab, stickPos, Quaternion.identity);
    }

    void CreatePlatform()
    {
        var currentPlatform = Instantiate(pillarPrefab);
        currentPillar = nextPillar == null ? currentPlatform : nextPillar;
        nextPillar = currentPlatform;
        currentPlatform.transform.position = pillarPrefab.transform.position + startPos;
        Vector3 tempDistance = new Vector3(Random.Range(spawnRange.x, spawnRange.y) + currentPillar.transform.localScale.x * 0.5f, 0, 0);
        startPos += tempDistance;
    }

    void SetRandomSize(GameObject pillar)
    {
        var newScale = pillar.transform.localScale;
        var allowedScale = nextPillar.transform.position.x - currentPillar.transform.position.x
            - currentPillar.transform.localScale.x * 0.5f - 0.4f;
        newScale.x = Mathf.Max(minMaxRange.x, Random.Range(minMaxRange.x, Mathf.Min(allowedScale, minMaxRange.y)));
        pillar.transform.localScale = newScale;
    }

    void UpdateScore()
    {
        score++;
        scoreText.text = score.ToString();
    }

    void GameOver()
    {
        endPanel.SetActive(true);
        scorePanel.SetActive(false);
        pausePanel.SetActive(false);

        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("HighScore", highScore);
        }

        scoreEndText.text = score.ToString();
        highScoreText.text = highScore.ToString();
    }

    public void UpdateDiamonds()
    {
        diamonds++;
        PlayerPrefs.SetInt("Diamonds", diamonds);
        diamondsText.text = diamonds.ToString();
    }

    public void GameStart()
    {
        startPanel.SetActive(false);
        scorePanel.SetActive(true);
        pausePanel.SetActive(false);

        CreatePlatform();
        SetRandomSize(nextPillar);
        currentState = GameState.INPUT;
    }

    public void GameRestart()
    {
        StateManager.instance.hasSceneStarted = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public void SceneRestart()
    {
        StateManager.instance.hasSceneStarted = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    // Pause and resume functionality
    public void TogglePause()
    {
        if (currentState == GameState.PAUSED)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        currentState = GameState.PAUSED;
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
        scorePanel.SetActive(false);
    }

    public void ResumeGame()
    {
        currentState = GameState.INPUT;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
        scorePanel.SetActive(true);
    }

    // Helper function to set player animation state
    private void SetPlayerAnimation(bool isMoving)
    {
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isMoving", isMoving);
        }
    }

    // Helper function to play sound
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Helper Functions
    IEnumerator Move(Transform currentTransform, Vector3 target, float time)
    {
        var passed = 0f;
        var init = currentTransform.transform.position;
        while (passed < time)
        {
            passed += Time.deltaTime;
            var normalized = passed / time;
            var current = Vector3.Lerp(init, target, normalized);
            currentTransform.position = current;
            yield return null;
        }
    }

    IEnumerator Rotate(Transform currentTransform, Transform target, float time)
    {
        var passed = 0f;
        var init = currentTransform.transform.rotation;
        while (passed < time)
        {
            passed += Time.deltaTime;
            var normalized = passed / time;
            var current = Quaternion.Slerp(init, target.rotation, normalized);
            currentTransform.rotation = current;
            yield return null;
        }
    }
}