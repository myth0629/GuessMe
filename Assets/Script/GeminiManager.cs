using UnityEngine;
using GenerativeAI;
using System.Threading.Tasks;
using TMPro;

public class GeminiManager : MonoBehaviour
{
    [Header("Gemini 설정")]
    [SerializeField] private GeminiApiKeySO apiKey;

    [Header("UI")]
    [Tooltip("현재 상황(내레이션) 표시용 텍스트")]
    public TextMeshProUGUI situationText;

    [Tooltip("선택지 4개를 표시할 텍스트들 (버튼 텍스트 등)")]
    public TextMeshProUGUI[] choiceTexts; // 0~3 인덱스 사용

    [Tooltip("남은 턴 수 표시용 텍스트")]
    public TextMeshProUGUI turnsRemainingText;

    // 간단한 게임 상태 구조체
    private GameState gameState;
    private GenerativeModel flashModel;

    private async void Start()
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

        // 모델은 flash 하나만 사용
        flashModel = new GenerativeModel(apiKey.apiKey, "gemini-2.5-flash-lite");

        // 게임 시작 시 GameState 초기화
        InitGameState();

        // 첫 턴 실행
        await RunTurnAsync();
    }

    private void InitGameState()
    {
        // AI가 완전히 자유롭게 시작 상황을 만들도록 최소한의 초기 상태만 제공
        gameState = new GameState
        {
            scene = "미정",
            objective = "생존하라",
            resources = new ResourcesState { medicine = 10, food = 20 },
            survivorGroups = new SurvivorGroupsState { doctors = 0, patients = 0, guards = 0 },
            plotSummary = "새로운 위기 상황이 시작되었다. 당신은 중요한 결정을 내려야 한다.",
            lastPlayerAction = "GameStart",
            turnsRemaining = 14 // 7일 = 14턴
        };
    }

    /// <summary>
    /// 한 턴: Flash 한 번 호출로 상황 + 선택지를 JSON으로 받아 파싱
    /// </summary>
    public async Task RunTurnAsync()
    {
        try
        {
            // GeminiPromptBuilder로 통합 프롬프트 생성 (JSON 응답 기대)
            string prompt = GeminiPromptBuilder.BuildUnifiedPrompt(gameState);
            var response = await flashModel.GenerateContentAsync(prompt);
            string rawText = response.Text;

            Debug.Log($"[Gemini Raw Response]\n{rawText}");

            // JSON 파싱
            GeminiResponse geminiResponse = ParseGeminiResponse(rawText);

            if (geminiResponse == null)
            {
                throw new System.Exception("JSON 파싱 실패: 응답 형식이 올바르지 않습니다.");
            }

            // UI 업데이트를 메인 스레드에서 확실히 실행
            UnityEngine.Debug.Log("[파싱 성공] UI 업데이트 시작...");
            UpdateUI(geminiResponse);
            UpdateTurnsUI(); // 남은 턴 UI 업데이트
        }
        catch (System.Exception e)
        {
            Debug.LogError($"턴 진행 중 에러 발생: {e.Message}");
            if (situationText != null)
            {
                string userMessage = "오류가 발생했습니다.";
                if (e.Message != null && e.Message.Contains("503"))
                {
                    userMessage = "서버가 잠시 과부하 상태입니다.\n잠시 후 다시 시도해 주세요.";
                }
                situationText.text = userMessage;
            }
        }
    }

    /// <summary>
    /// 버튼 클릭 시 호출 (유니티 이벤트에서 index 0~3 전달)
    /// </summary>
    public async void OnChoiceSelected(int index)
    {
        if (choiceTexts == null || index < 0 || index >= choiceTexts.Length || choiceTexts[index] == null)
            return;

        // 마지막 플레이어 행동 갱신
        gameState.lastPlayerAction = choiceTexts[index].text;

        // 턴 감소
        gameState.turnsRemaining--;

        // 간단한 규칙 예시: 선택에 따라 리소스 변경 (필요 시 확장)
        gameState.resources.medicine = Mathf.Max(0, gameState.resources.medicine - 1);
        gameState.resources.food = Mathf.Max(0, gameState.resources.food - 1);

        // plotSummary 갱신
        gameState.plotSummary = $"플레이어의 최근 선택: {gameState.lastPlayerAction}";

        // 다음 턴 진행
        await RunTurnAsync();
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
            turnsRemainingText.text = $"Day {8 - daysRemaining} {timeOfDay} (남은 턴: {gameState.turnsRemaining})";
        }
    }
}

// --- GameState 및 응답 DTO 정의 ---

[System.Serializable]
public class GameState
{
    public string scene;
    public string objective;
    public ResourcesState resources;
    public SurvivorGroupsState survivorGroups;
    public string plotSummary;
    public string lastPlayerAction;
    public int turnsRemaining; // 남은 턴 수
}

[System.Serializable]
public class ResourcesState
{
    public int medicine;
    public int food;
}

[System.Serializable]
public class SurvivorGroupsState
{
    public int doctors;
    public int patients;
    public int guards;
}

[System.Serializable]
public class GeminiResponse
{
    public string situation_text;
    public string[] choices;
}

