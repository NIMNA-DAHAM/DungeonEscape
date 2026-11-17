using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Central game state: score, key progress, win/lose panels, restart, and UI feedback.
/// </summary>
public class GameManager : MonoBehaviour
{
    private const int MaxLives = 10;
    private const int MaxLevel = 8;

    public static GameManager Instance { get; private set; }

    private static int currentLevel = 1;
    private static bool continueSession;
    private static int carriedScore;
    private static int carriedLives;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI informationText;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Beginner Settings")]
    [SerializeField, Min(1)] private int startingLives = 5;
    [SerializeField, Min(1f)] private float timeLimit = 60f;
    [SerializeField, Min(0.1f)] private float hitProtectionSeconds = 1.5f;

    [Header("Starting Message")]
    [SerializeField] private string startMessage = "Find the key and escape!";

    private int score;
    private int keysCollected;
    private int requiredKeys = 1;
    private bool isPlaying = true;
    private int lives;
    private int LifeCapacity => MaxLives;
    private float timeRemaining;
    private float nextDamageTime;
    private bool levelCompleted;

    public bool IsPlaying => isPlaying;
    public bool HasKey => keysCollected > 0;
    public bool HasAllKeys => keysCollected >= requiredKeys;
    public string KeyRequirementMessage => requiredKeys > 1
        ? "Collect both keys first! (" + keysCollected + "/" + requiredKeys + ")"
        : "Collect the key first!";
    public static int CurrentLevel => currentLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        currentLevel = 1;
        continueSession = false;
        carriedScore = 0;
        carriedLives = 0;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GetComponent<UiAnimator>() == null)
        {
            gameObject.AddComponent<UiAnimator>();
        }
    }

    private void Start()
    {
        currentLevel = Mathf.Clamp(currentLevel, 1, MaxLevel);
        requiredKeys = currentLevel >= 3 ? 2 : 1;
        DungeonObjectRandomizer.RandomizePositions(currentLevel);
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.orthographic)
        {
            mainCamera.orthographicSize = currentLevel == 7 ? 8f : 5f;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPlaying = true;
        keysCollected = 0;
        levelCompleted = false;
        score = continueSession ? carriedScore : 0;
        lives = continueSession ? Mathf.Clamp(carriedLives, 1, LifeCapacity) :
            Mathf.Clamp(startingLives, 1, LifeCapacity);
        continueSession = false;
        timeRemaining = Mathf.Max(1f, timeLimit);
        nextDamageTime = 0f;
        UpdateLivesUI();
        UpdateTimerUI();

        UpdateScoreUI();
        ShowMessage(currentLevel == 1 ? startMessage :
            currentLevel == 2 ? "LEVEL 2 - Two enemies guard the exit!" :
            currentLevel == 3 ? "LEVEL 3 - Find both keys while an enemy circles the dungeon!" :
            currentLevel == 4 ? "LEVEL 4 - Cross the enemy routes, find both keys, and claim the extra heart!" :
            currentLevel == 5 ? "LEVEL 5 - Find both keys in the maze and escape the patrolling enemies!" :
            currentLevel == 6 ? "LEVEL 6 - Time your way past two moving gates and the dungeon guard!" :
            currentLevel == 7 ? "LEVEL 7 - Explore the expanded maze, collect both keys, and find the exit!" :
            "LEVEL 8 - Time three moving gates while avoiding three slow-moving guards!");
        CreateLevelSelector();
        SetEndScreenButtonLabel("Restart");

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.RemoveListener(HandleEndScreenButton);
            restartButton.onClick.AddListener(HandleEndScreenButton);
            restartButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isPlaying) return;

        // Delta time makes the countdown independent of the frame rate.
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerUI();
        if (timeRemaining <= 0f)
        {
            TriggerGameOver();
            ShowMessage("Time's up!");
        }
    }

    public void DamagePlayer(PlayerMovement player)
    {
        // Ignore repeated contacts briefly after a hit.
        if (!isPlaying || player == null || Time.time < nextDamageTime) return;

        nextDamageTime = Time.time + hitProtectionSeconds;
        lives--;
        UpdateLivesUI();
        if (lives <= 0)
        {
            TriggerGameOver();
            return;
        }

        player.Respawn();
        ShowMessage("Ouch! Back to the start. Lives left: " + lives);
    }

    public bool TryRestoreLife()
    {
        if (!isPlaying || lives >= LifeCapacity) return false;

        lives = Mathf.Min(lives + 1, LifeCapacity);
        UpdateLivesUI();
        ShowMessage("Extra heart collected! Lives: " + lives);
        return true;
    }

    public void AddTime(float seconds)
    {
        if (!isPlaying || seconds <= 0f) return;

        timeRemaining += seconds;
        UpdateTimerUI();
    }

    private void UpdateLivesUI()
    {
        if (livesText != null) livesText.text = "Lives: " + lives;
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        timerText.text = "Time: " + Mathf.CeilToInt(timeRemaining) + "s";
        timerText.color = timeRemaining <= 10f ? Color.red : Color.white;
    }

    public void CollectKey(int points)
    {
        if (!isPlaying)
        {
            return;
        }

        if (HasAllKeys)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayKeyCollect();
        }

        keysCollected++;
        AddScore(points);
        ShowMessage(HasAllKeys
            ? "All keys collected! Find the exit!"
            : requiredKeys > 1
                ? "Key " + keysCollected + "/" + requiredKeys + " collected! Find the other key!"
                : "Key collected! Find the exit!");
    }

    public void AddScore(int points)
    {
        if (!isPlaying) return;
        score += points;
        UpdateScoreUI();

        if (UiAnimator.Instance != null && scoreText != null)
        {
            UiAnimator.Instance.PunchScale(scoreText.transform, 0.35f, 0.3f);
        }
    }

    public void ShowMessage(string message)
    {
        if (informationText == null)
        {
            return;
        }

        if (UiAnimator.Instance != null)
        {
            UiAnimator.Instance.FlashMessage(informationText, message);
        }
        else
        {
            informationText.text = message;
        }
    }

    public void TriggerWin()
    {
        if (!isPlaying)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWin();
        }

        AddScore(Mathf.FloorToInt(timeRemaining) * 2);
        EndGame();
        levelCompleted = true;
        bool hasNextLevel = currentLevel < MaxLevel;
        ShowMessage(hasNextLevel ? "Level " + currentLevel + " complete!" : "All eight levels complete!");
        SetEndScreenButtonLabel(hasNextLevel ? "Next Level" : "Play Again");
        SetWinPanelMessage(hasNextLevel ? "LEVEL " + currentLevel + " COMPLETE!" : "ALL EIGHT LEVELS COMPLETE!");

        if (UiAnimator.Instance != null)
        {
            UiAnimator.Instance.ShowPanelAnimated(winPanel);
        }
        else if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
    }

    public void TriggerGameOver()
    {
        if (!isPlaying)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGameOver();
        }

        levelCompleted = false;
        EndGame();
        ShowMessage("GAME OVER!");
        SetEndScreenButtonLabel("Restart");

        if (UiAnimator.Instance != null)
        {
            UiAnimator.Instance.ShowPanelAnimated(gameOverPanel);
        }
        else if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleEndScreenButton()
    {
        if (levelCompleted && currentLevel < MaxLevel)
        {
            carriedScore = score;
            carriedLives = startingLives;
            continueSession = true;
            currentLevel++;
        }
        else if (levelCompleted)
        {
            currentLevel = 1;
            continueSession = false;
        }

        RestartGame();
    }

    private void SetEndScreenButtonLabel(string label)
    {
        if (restartButton == null) return;

        TextMeshProUGUI buttonText = restartButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = label;
        }
    }

    private void SetWinPanelMessage(string message)
    {
        if (winPanel == null) return;

        TextMeshProUGUI panelText = winPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (panelText != null)
        {
            panelText.text = message;
        }
    }

    private void CreateLevelSelector()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DungeonEscape] Cannot create the level selector because the scene has no Canvas.", this);
            return;
        }

        GameObject panelObject = new GameObject("LevelSelectorPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-16f, 0f);
        panelRect.sizeDelta = new Vector2(210f, 540f);
        panelObject.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.9f);

        TMP_FontAsset font = scoreText != null ? scoreText.font : TMP_Settings.defaultFontAsset;
        CreateLevelSelectorLabel(panelRect, "Title", "SELECT LEVEL", 20f, 220f, font);

        for (int i = 0; i < MaxLevel; i++)
        {
            int selectedLevel = i + 1;
            GameObject buttonObject = new GameObject(
                "Level" + selectedLevel + "Button",
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panelRect, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, (MaxLevel - 1) * 24f - i * 48f);
            buttonRect.sizeDelta = new Vector2(174f, 50f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = selectedLevel == currentLevel
                ? new Color(0.15f, 0.55f, 0.72f, 1f)
                : new Color(0.2f, 0.23f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.25f, 0.65f, 0.8f, 1f);
            colors.pressedColor = new Color(0.1f, 0.4f, 0.56f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.2f, 0.23f, 0.3f, 0.5f);
            button.colors = colors;

            CreateLevelSelectorLabel(buttonRect, "Label", "LEVEL " + selectedLevel, 20f, 0f, font);
            button.onClick.AddListener(() => SelectLevel(selectedLevel));
        }
    }

    private static void CreateLevelSelectorLabel(RectTransform parent, string name, string text,
        float fontSize, float verticalPosition, TMP_FontAsset font)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, verticalPosition);
        labelRect.sizeDelta = new Vector2(190f, 36f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        if (font != null)
        {
            label.font = font;
        }
    }

    private void SelectLevel(int level)
    {
        if (level < 1 || level > MaxLevel)
        {
            Debug.LogError("[DungeonEscape] Level selection is outside the available level range: " + level, this);
            return;
        }

        currentLevel = level;
        continueSession = false;
        carriedScore = 0;
        carriedLives = 0;
        levelCompleted = false;
        RestartGame();
    }

    private void EndGame()
    {
        isPlaying = false;
        // Pause gameplay, but keep UI / audio animations on unscaled time.
        Time.timeScale = 0f;

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(true);
            if (UiAnimator.Instance != null)
            {
                UiAnimator.Instance.PunchScale(restartButton.transform, 0.2f, 0.35f);
            }
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(HandleEndScreenButton);
        }
    }
}
