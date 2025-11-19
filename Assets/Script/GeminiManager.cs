using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using UnityEngine.UI;
using TMPro;

public class GeminiManager : MonoBehaviour
{
    [Header("Gemini 설정")]
    [SerializeField] private GeminiApiKeySO apiKey;

    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string ModelName = "gemini-2.5-flash-lite";

    [Header("UI")]
    [Tooltip("현재 상황(내레이션) 표시용 텍스트")]
    public TextMeshProUGUI situationText;

    [Tooltip("선택지 4개를 표시할 텍스트들 (버튼 텍스트 등)")]
    public TextMeshProUGUI[] choiceTexts; // 0~3 인덱스 사용

    [Tooltip("남은 턴 수 표시용 텍스트")]
    public TextMeshProUGUI turnsRemainingText;

    [Header("안정성 UI")]
    [Tooltip("안정도 게이지 (0~100)")]
    public Slider StabilitySlider;

    [Tooltip("안정도 값 텍스트 (예: 70/100)")]
    public TextMeshProUGUI stabilityValueText;

    [Header("로딩 UI")]
    [Tooltip("로딩 패널 (API 호출 중 표시)")]
    public GameObject loadingPanel;

    [Tooltip("선택지 버튼들 (로딩 중 비활성화)")]
    public Button[] choiceButtons; // 0~3 인덱스 사용

    // 간단한 게임 상태 구조체
    private GameState gameState;
    private bool _isProcessing = false; // API 처리 중 플래그
    private bool _isFirstTurn = true; // 첫 번째 턴 여부 (첫 턴은 상태 변화 없음)

    private const string SelectedThemeKey = "SelectedTheme";

    private void Start()
    {
        if (string.IsNullOrEmpty(apiKey.apiKey))
        {
            Debug.LogError("GeminiTest: API 키가 비어 있습니다.");
            if (situationText != null)
            {
                situationText.text = "오류: API 키가 설정되지 않았습니다.";
            }
            return;
        }

        // 로딩 패널 초기 상태 설정
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        // 슬라이더 범위 초기화 (0~100)
        InitializeSliders();

        // 게임 시작 시 GameState 초기화
        InitGameState();

        // 초기 UI 업데이트
        UpdateStatsUI();

        // 첫 턴 실행
        StartCoroutine(RunTurnCoroutine());
    }

    private void InitGameState()
    {
        string selectedTheme = PlayerPrefs.GetString(SelectedThemeKey, "Random");

        // Random은 그대로 유지 - GeminiPromptBuilder에서 새로운 테마를 생성하도록 함
        Debug.Log($"� 게임 시작 - 선택된 테마: {selectedTheme}");

        // AI가 완전히 자유롭게 시작 상황을 만들도록 최소한의 초기 상태만 제공
        gameState = new GameState
        {
            scene = "미정",
            objective = "목표를 달성하라",
            survivorGroups = new SurvivorGroupsState { doctors = 0, patients = 0, guards = 0 },
            plotSummary = "새로운 딜레마 상황이 시작되었다. 당신은 중요한 결정을 내려야 한다.",
            lastPlayerAction = "GameStart",
            turnsRemaining = 14, // 7일 = 14턴
            stability = new StabilityState { stability = 100 },
            selectedTheme = selectedTheme
        };
    }

    /// <summary>
    /// 슬라이더 범위 초기화 (0~100)
    /// </summary>
    private void InitializeSliders()
    {
        // 안정성 슬라이더
        if (StabilitySlider != null)
        {
            StabilitySlider.minValue = 0;
            StabilitySlider.maxValue = 100;
        }

        Debug.Log("✅ 슬라이더 범위 초기화 완료 (0~100)");
    }

    /// <summary>
    /// 한 턴: Gemini API 호출로 상황 + 선택지를 JSON으로 받아 파싱
    /// </summary>
    private IEnumerator RunTurnCoroutine()
    {
        // 로딩 시작
        SetLoadingState(true);

        // GeminiPromptBuilder로 통합 프롬프트 생성 (JSON 응답 기대)
        string prompt = GeminiPromptBuilder.BuildUnifiedPrompt(gameState);

        // Gemini API 호출
        yield return StartCoroutine(CallGeminiAPI(prompt, (response) =>
        {
            if (response == null)
            {
                Debug.LogError("API 응답이 null입니다.");
                if (situationText != null)
                {
                    situationText.text = "오류가 발생했습니다.";
                }
                SetLoadingState(false);
                return;
            }

            // UI 업데이트를 메인 스레드에서 확실히 실행
            UnityEngine.Debug.Log("[파싱 성공] UI 업데이트 시작...");
            UpdateUI(response);
            UpdateTurnsUI(); // 남은 턴 UI 업데이트
            
            // 첫 번째 턴이 아닐 때만 상태 업데이트 적용
            if (!_isFirstTurn)
            {
                ApplyStateUpdate(response); // 상태 업데이트 적용
            }
            else
            {
                Debug.Log("🎮 첫 번째 턴: 상태 변화 없음 (초기 상황만 표시)");
            }
            
            UpdateStatsUI(); // 안정성, 신뢰도, 자원 UI 업데이트

            // 안정성 체크 (게임오버 조건)
            if (gameState.stability.stability <= 0)
            {
                Debug.Log("안정성이 0이 되었습니다! 게임오버!");
                
                // 로딩 종료 및 버튼 비활성화
                SetLoadingState(false);
                DisableChoiceButtons();
                
                // API를 통해 게임 오버 상황 설명 생성
                StartCoroutine(ShowGameOverMessageCoroutine());
                return;
            }

            // 로딩 종료
            SetLoadingState(false);
        }));
    }

    /// <summary>
    /// Gemini API 호출 (UnityWebRequest 사용)
    /// </summary>
    private IEnumerator CallGeminiAPI(string prompt, System.Action<GeminiResponse> callback)
    {
        string url = $"{BaseUrl}{ModelName}:generateContent?key={apiKey.apiKey}";

        // JSON 요청 본문 생성
        string escapedPrompt = EscapeJsonString(prompt);
        string jsonBody = $"{{\"contents\":[{{\"parts\":[{{\"text\":\"{escapedPrompt}\"}}]}}]}}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API 호출 실패: {request.error}");
                Debug.LogError($"응답 코드: {request.responseCode}");
                
                callback?.Invoke(null);
            }
            else
            {
                string rawResponse = request.downloadHandler.text;

                // API 응답에서 텍스트 추출
                string extractedText = ExtractTextFromApiResponse(rawResponse);
                
                if (string.IsNullOrEmpty(extractedText))
                {
                    Debug.LogError("API 응답에서 텍스트를 추출할 수 없습니다.");
                    callback?.Invoke(null);
                }
                else
                {
                    // JSON 파싱
                    GeminiResponse geminiResponse = ParseGeminiResponse(extractedText);
                    callback?.Invoke(geminiResponse);
                }
            }
        }
    }

    /// <summary>
    /// API 응답에서 실제 텍스트 부분 추출
    /// </summary>
    private string ExtractTextFromApiResponse(string apiResponse)
    {
        try
        {
            // JsonUtility를 사용하여 안전하게 파싱
            GeminiApiResponse response = JsonUtility.FromJson<GeminiApiResponse>(apiResponse);
            
            if (response != null && 
                response.candidates != null && response.candidates.Length > 0 &&
                response.candidates[0].content != null && 
                response.candidates[0].content.parts != null && response.candidates[0].content.parts.Length > 0)
            {
                return response.candidates[0].content.parts[0].text;
            }
            
            Debug.LogError("API 응답 구조가 예상과 다릅니다.");
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"텍스트 추출 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSON 문자열 이스케이프 처리
    /// </summary>
    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        
        StringBuilder sb = new StringBuilder(str.Length + 100);
        
        foreach (char c in str)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    // 제어 문자 처리
                    if (c < 32)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 한 턴 실행 (외부 호출용 - 기존 호환성 유지)
    /// </summary>
    public void RunTurnAsync()
    {
        StartCoroutine(RunTurnCoroutine());
    }

    /// <summary>
    /// 버튼 클릭 시 호출 (유니티 이벤트에서 index 0~3 전달)
    /// </summary>
    public void OnChoiceSelected(int index)
    {
        // 이미 처리 중이면 무시
        if (_isProcessing)
        {
            Debug.LogWarning("이미 선택 처리 중입니다.");
            return;
        }

        if (choiceTexts == null || index < 0 || index >= choiceTexts.Length || choiceTexts[index] == null)
            return;

        // 마지막 플레이어 행동 갱신
        gameState.lastPlayerAction = choiceTexts[index].text;

        // 첫 번째 턴이었다면 이제 두 번째 턴으로 전환
        if (_isFirstTurn)
        {
            _isFirstTurn = false;
            Debug.Log("✅ 첫 번째 선택 완료! 다음 턴부터 상태 변화가 적용됩니다.");
        }

        // 턴 감소
        gameState.turnsRemaining--;

        // plotSummary 갱신
        gameState.plotSummary = $"플레이어의 최근 선택: {gameState.lastPlayerAction}";

        // 안정성 0 확인 (즉시 게임오버)
        if (gameState.stability.stability <= 0)
        {
            Debug.Log("안정성 0! 게임오버!");
            DisableChoiceButtons();
            StartCoroutine(ShowGameOverMessageCoroutine());
            return;
        }

        // 14턴(Day 7 오후) 종료 확인
        if (gameState.turnsRemaining <= 0)
        {
            // 게임 종료 - 결산 씬으로 이동
            GoToResultScene();
            return;
        }

        // 다음 턴 진행
        StartCoroutine(RunTurnCoroutine());
    }

    /// <summary>
    /// 게임 오버 시 API를 통해 상황 설명 생성 및 표시
    /// </summary>
    private IEnumerator ShowGameOverMessageCoroutine()
    {
        Debug.Log("게임 오버 메시지 생성 중...");

        // 게임 오버 프롬프트 생성
        string prompt = GeminiPromptBuilder.BuildGameOverPrompt(gameState);

        // API 호출
        yield return StartCoroutine(CallGeminiAPI(prompt, (response) =>
        {
            if (response != null && !string.IsNullOrEmpty(response.situation_text))
            {
                // 게임 오버 텍스트를 situation_text에서 가져옴
                if (situationText != null)
                {
                    situationText.text = response.situation_text;
                }
                Debug.Log($"[게임 오버] {response.situation_text}");
            }
            else
            {
                // 기본 메시지 표시
                if (situationText != null)
                {
                    situationText.text = "안정성이 바닥났습니다. 모든 것이 무너졌습니다...";
                }
            }
        }));

        // 3초 대기 후 결산 씬으로
        yield return new WaitForSeconds(3f);
        GoToResultScene();
    }

    /// <summary>
    /// 결산 씬으로 이동
    /// </summary>
    private void GoToResultScene()
    {
        // GameState를 PlayerPrefs에 저장하여 결산 씬에서 사용
        string gameStateJson = JsonUtility.ToJson(gameState);
        PlayerPrefs.SetString("FinalGameState", gameStateJson);
        PlayerPrefs.Save();

        Debug.Log("게임 종료! 결산 씬으로 이동합니다.");

        // SceneFadeManager를 통해 결산 씬으로 전환
        if (SceneFadeManager.Instance != null)
        {
            SceneFadeManager.Instance.LoadSceneWithFade("ResultScene");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("ResultScene");
        }
    }

    /// <summary>
    /// 로딩 상태 설정 (로딩 패널 표시/숨김, 버튼 활성화/비활성화)
    /// </summary>
    private void SetLoadingState(bool isLoading)
    {
        _isProcessing = isLoading;

        // 로딩 패널 표시/숨김
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(isLoading);
        }

        // 선택지 버튼 활성화/비활성화
        if (choiceButtons != null)
        {
            foreach (var button in choiceButtons)
            {
                if (button != null)
                {
                    button.interactable = !isLoading;
                }
            }
        }

        Debug.Log($"로딩 상태: {(isLoading ? "로딩 중..." : "로딩 완료")}");
    }

    /// <summary>
    /// 선택지 버튼 비활성화 (게임 오버 시)
    /// </summary>
    private void DisableChoiceButtons()
    {
        if (choiceButtons != null)
        {
            foreach (var button in choiceButtons)
            {
                if (button != null)
                {
                    button.interactable = false;
                }
            }
        }

        Debug.Log("선택지 버튼 비활성화됨 (게임 오버)");
    }

    /// <summary>
    /// UI 업데이트 (메인 스레드에서 확실히 실행)
    /// </summary>
    private void UpdateUI(GeminiResponse response)
    {
        if (response == null)
        {
            Debug.LogError("UpdateUI: response가 null입니다!");
            return;
        }

        // 상황 텍스트 업데이트
        if (situationText != null)
        {
            situationText.text = response.situation_text;
            Debug.Log($"✅ [UI 업데이트 완료] 상황 텍스트: {response.situation_text.Substring(0, Mathf.Min(50, response.situation_text.Length))}...");
        }
        else
        {
            Debug.LogWarning("⚠️ situationText가 null입니다. 인스펙터에서 TMP 텍스트를 연결하세요!");
        }

        // 선택지 텍스트 업데이트
        if (choiceTexts == null || choiceTexts.Length == 0)
        {
            Debug.LogWarning("⚠️ choiceTexts 배열이 비어있습니다. 인스펙터에서 Size=4로 설정하고 버튼 텍스트를 드래그하세요!");
            return;
        }

        for (int i = 0; i < choiceTexts.Length; i++)
        {
            if (i < response.choices.Length && choiceTexts[i] != null)
            {
                choiceTexts[i].text = response.choices[i];
                Debug.Log($"✅ [UI 업데이트 완료] 선택지 {i}: {response.choices[i].Substring(0, Mathf.Min(40, response.choices[i].Length))}...");
            }
            else if (choiceTexts[i] != null)
            {
                choiceTexts[i].text = "";
            }
            else if (i < response.choices.Length)
            {
                Debug.LogWarning($"⚠️ choiceTexts[{i}]가 null입니다. 인스펙터에서 연결하세요!");
            }
        }
    }

    private GeminiResponse ParseGeminiResponse(string rawText)
    {
        try
        {
            // 혹시 모델이 ```json ... ``` 형태로 감싸서 반환하는 경우 제거
            string cleaned = rawText.Trim();
            if (cleaned.StartsWith("```json"))
            {
                cleaned = cleaned.Substring(7);
            }
            if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Substring(3);
            }
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            }
            cleaned = cleaned.Trim();

            GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(cleaned);
            return response;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON 파싱 오류: {e.Message}\nRaw Text:\n{rawText}");
            return null;
        }
    }

    /// <summary>
    /// 남은 턴 수 UI 업데이트
    /// </summary>
    private void UpdateTurnsUI()
    {
        if (turnsRemainingText != null)
        {
            int daysRemaining = Mathf.CeilToInt(gameState.turnsRemaining / 2f);
            string timeOfDay = (gameState.turnsRemaining % 2 == 0) ? "오전" : "오후";
            turnsRemainingText.text = $"Day {8 - daysRemaining} {timeOfDay}";
        }
    }

    /// <summary>
    /// Gemini 응답에서 상태 업데이트 적용
    /// </summary>
    private void ApplyStateUpdate(GeminiResponse response)
    {
        if (response.state_update == null)
            return;

        // 안정성 업데이트
        if (response.state_update.stability != null)
        {
            int oldStability = gameState.stability.stability;
            int newStability = response.state_update.stability.stability;
            
            // 안정성 변화량 제한 (한 턴에 최대 ±20)
            int stabilityChange = newStability - oldStability;
            if (Mathf.Abs(stabilityChange) > 20)
            {
                Debug.LogWarning($"⚠️ 안정성 변화량이 너무 큽니다! ({oldStability} → {newStability}, 변화량: {stabilityChange}). 최대 ±20으로 제한합니다.");
                stabilityChange = Mathf.Clamp(stabilityChange, -20, 20);
                newStability = oldStability + stabilityChange;
            }
            
            gameState.stability.stability = Mathf.Clamp(newStability, 0, 100);
            Debug.Log($"[안정성 업데이트] {oldStability} → {gameState.stability.stability} (변화량: {gameState.stability.stability - oldStability})");
        }
    }

    /// <summary>
    /// 안정성, 자원 UI 업데이트
    /// </summary>
    private void UpdateStatsUI()
    {
        // 안정성 게이지 업데이트
        if (StabilitySlider != null)
        {
            StabilitySlider.value = gameState.stability.stability;
            Debug.Log($"[UI 슬라이더] 안정성 슬라이더 = {gameState.stability.stability} (minValue={StabilitySlider.minValue}, maxValue={StabilitySlider.maxValue})");
        }
        else
        {
            Debug.LogWarning("⚠️ StabilitySlider가 null입니다!");
        }

        // 안정성 값 텍스트 업데이트
        if (stabilityValueText != null)
        {
            stabilityValueText.text = $"{gameState.stability.stability}/100";
        }
    }
}

// --- GameState 및 응답 DTO 정의 ---

[System.Serializable]
public class GameState
{
    public string scene;
    public string objective;
    public SurvivorGroupsState survivorGroups;
    public string plotSummary;
    public string lastPlayerAction;
    public int turnsRemaining; // 남은 턴 수
    public StabilityState stability; // 안정성 지표
    public string selectedTheme;
}

[System.Serializable]
public class SurvivorGroupsState
{
    public int doctors;
    public int patients;
    public int guards;
}

[System.Serializable]
public class StabilityState
{
    public int stability; // 안정도 (0~100)
}

[System.Serializable]
public class GeminiResponse
{
    public string situation_text;
    public string[] choices;
    public GameStateUpdate state_update; // 상태 업데이트 정보
}

[System.Serializable]
public class GameStateUpdate
{
    public StabilityUpdate stability;
}

[System.Serializable]
public class StabilityUpdate
{
    public int stability;
}

// --- Gemini API Response Wrapper ---

[System.Serializable]
public class GeminiApiResponse
{
    public Candidate[] candidates;
}

[System.Serializable]
public class Candidate
{
    public Content content;
}

[System.Serializable]
public class Content
{
    public Part[] parts;
}

[System.Serializable]
public class Part
{
    public string text;
}
