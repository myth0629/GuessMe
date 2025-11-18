using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 범용 씬 전환 UI 매니저
/// StartScene, ResultScene 등 다양한 씬에서 사용 가능
/// </summary>
public class StartSceneUI : MonoBehaviour
{
    [Header("버튼 설정")]
    [Tooltip("게임 시작/재시작 버튼")]
    [SerializeField] private Button _startButton;

    [Tooltip("메인 메뉴로 돌아가기 버튼")]
    [SerializeField] private Button _mainMenuButton;

    [Tooltip("게임 종료 버튼 (선택사항)")]
    [SerializeField] private Button _quitButton;

    [Header("씬 설정")]
    [Tooltip("시작 버튼 클릭 시 로드할 씬 이름")]
    [SerializeField] private string _targetSceneName = "GameScene";

    [Tooltip("메인 메뉴 버튼 클릭 시 로드할 씬 이름")]
    [SerializeField] private string _mainMenuSceneName = "StartScene";

    private void Start()
    {
        // 버튼 이벤트 연결
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }

        if (_mainMenuButton != null)
        {
            _mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(OnQuitButtonClicked);
        }
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지를 위한 이벤트 해제
        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        if (_mainMenuButton != null)
        {
            _mainMenuButton.onClick.RemoveListener(OnMainMenuButtonClicked);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.RemoveListener(OnQuitButtonClicked);
        }
    }

    /// <summary>
    /// 시작/재시작 버튼 클릭 시 호출
    /// </summary>
    private void OnStartButtonClicked()
    {
        Debug.Log($"씬 이동: {_targetSceneName}");
        LoadScene(_targetSceneName);
    }

    /// <summary>
    /// 메인 메뉴 버튼 클릭 시 호출
    /// </summary>
    private void OnMainMenuButtonClicked()
    {
        Debug.Log($"메인 메뉴로 이동: {_mainMenuSceneName}");
        
        // 저장된 게임 상태 초기화 (결산 씬에서 돌아갈 때)
        PlayerPrefs.DeleteKey("FinalGameState");
        
        LoadScene(_mainMenuSceneName);
    }

    /// <summary>
    /// 게임 종료 버튼 클릭 시 호출
    /// </summary>
    private void OnQuitButtonClicked()
    {
        Debug.Log("게임 종료");
        QuitGame();
    }

    /// <summary>
    /// 씬 로드 (페이드 효과 포함)
    /// </summary>
    private void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            // 페이드 효과와 함께 씬 전환
            if (SceneFadeManager.Instance != null)
            {
                SceneFadeManager.Instance.LoadSceneWithFade(sceneName);
            }
            else
            {
                // 페이드 매니저가 없으면 일반 로드
                Debug.LogWarning("⚠️ SceneFadeManager가 없습니다. 일반 씬 전환을 수행합니다.");
                SceneManager.LoadScene(sceneName);
            }
        }
        else
        {
            Debug.LogError("❌ 씬 이름이 설정되지 않았습니다!");
        }
    }

    /// <summary>
    /// 게임 종료
    /// </summary>
    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 외부에서 호출 가능한 public 메서드 (Unity Event용)
    /// 지정된 타겟 씬으로 이동
    /// </summary>
    public void StartGame()
    {
        LoadScene(_targetSceneName);
    }

    /// <summary>
    /// 외부에서 호출 가능한 public 메서드 (Unity Event용)
    /// 메인 메뉴로 이동
    /// </summary>
    public void GoToMainMenu()
    {
        PlayerPrefs.DeleteKey("FinalGameState");
        LoadScene(_mainMenuSceneName);
    }

    /// <summary>
    /// 외부에서 호출 가능한 public 메서드 (Unity Event용)
    /// 특정 씬으로 직접 이동
    /// </summary>
    public void LoadSceneByName(string sceneName)
    {
        LoadScene(sceneName);
    }

    /// <summary>
    /// 외부에서 호출 가능한 public 메서드 (Unity Event용)
    /// 게임 재시작 (게임 상태 초기화 포함)
    /// </summary>
    public void RestartGame()
    {
        PlayerPrefs.DeleteKey("FinalGameState");
        LoadScene("GameScene");
    }

    /// <summary>
    /// 외부에서 호출 가능한 public 메서드 (Unity Event용)
    /// 게임 종료
    /// </summary>
    public void ExitGame()
    {
        QuitGame();
    }
}
