using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

/// <summary>
/// 게임 결산 씬 UI 관리
/// </summary>
public class ResultSceneUI : MonoBehaviour
{
    [Header("Gemini API")]
    [SerializeField] private GeminiApiKeySO apiKey;

    [Header("결과 텍스트")]
    [Tooltip("게임 결과 제목 (예: '생존 성공!' 또는 '게임 오버')")]
    public TextMeshProUGUI resultTitleText;

    [Tooltip("최종 상태 요약 텍스트")]
    public TextMeshProUGUI summaryText;

    [Header("최종 스탯")]
    [Tooltip("안정도 슬라이더")]
    public Slider finalStabilitySlider;

    [Tooltip("안정도 값 텍스트")]
    public TextMeshProUGUI stabilityValueText;

    [Header("버튼")]
    [Tooltip("메인 메뉴로 버튼 (StartSceneUI 컴포넌트 사용)")]
    public StartSceneUI sceneNavigator;

    [Header("로딩 UI")]
    [Tooltip("로딩 패널 (API 호출 중 표시)")]
    public GameObject loadingPanel;

    private GameState _finalGameState;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string ModelName = "gemini-2.5-flash-lite";

    private void Start()
    {
        // PlayerPrefs에서 최종 게임 상태 로드
        string gameStateJson = PlayerPrefs.GetString("FinalGameState", "");
        
        if (string.IsNullOrEmpty(gameStateJson))
        {
            Debug.LogError("최종 게임 상태를 찾을 수 없습니다!");
            if (summaryText != null)
                summaryText.text = "게임 상태를 불러올 수 없습니다.";
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
            return;
        }

        _finalGameState = JsonUtility.FromJson<GameState>(gameStateJson);
        
        // 결과 표시 시작
        StartCoroutine(DisplayResultsCoroutine());
    }

    /// <summary>
    /// 최종 결과 표시
    /// </summary>
    private IEnumerator DisplayResultsCoroutine()
    {
        if (_finalGameState == null) yield break;

        // 로딩 패널 표시
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // Gemini API 초기화 체크
        if (string.IsNullOrEmpty(apiKey.apiKey))
        {
            Debug.LogError("ResultSceneUI: API 키가 비어 있습니다.");
            if (summaryText != null)
                summaryText.text = "오류: API 키가 설정되지 않았습니다.";
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
            yield break;
        }

        // 생존 성공 여부 판단 (안정성이 0보다 크면 성공)
        bool survived = _finalGameState.stability.stability > 0;

        // 안정도
        if (finalStabilitySlider != null)
        {
            finalStabilitySlider.value = _finalGameState.stability.stability;
        }
        if (stabilityValueText != null)
        {
            stabilityValueText.text = $"{_finalGameState.stability.stability}/100";
        }

        // AI로 제목과 요약 생성
        bool titleGenerated = false;
        bool summaryGenerated = false;

        // 제목 생성
        if (resultTitleText != null)
        {
            yield return StartCoroutine(GenerateAITitleCoroutine(survived, (title) =>
            {
                if (!string.IsNullOrEmpty(title))
                {
                    resultTitleText.text = title;
                    resultTitleText.color = survived ? Color.green : Color.red;
                    titleGenerated = true;
                }
            }));
        }

        // 요약 생성
        if (summaryText != null)
        {
            yield return StartCoroutine(GenerateAISummaryCoroutine(survived, (summary) =>
            {
                if (!string.IsNullOrEmpty(summary))
                {
                    summaryText.text = summary;
                    summaryGenerated = true;
                }
            }));
        }

        // 생성 실패 시 기본값 사용
        if (!titleGenerated && resultTitleText != null)
        {
            resultTitleText.text = survived ? "임무 완수" : "임무 실패";
            resultTitleText.color = survived ? Color.green : Color.red;
        }

        if (!summaryGenerated && summaryText != null)
        {
            summaryText.text = GenerateDetailedSummary(survived);
        }

        // 로딩 패널 숨김
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    /// <summary>
    /// AI를 통해 제목 생성
    /// </summary>
    private IEnumerator GenerateAITitleCoroutine(bool survived, System.Action<string> callback)
    {
        int stability = _finalGameState.stability.stability;
        int turnsPlayed = 14 - _finalGameState.turnsRemaining;

        string prompt = $@"당신은 게임 결과 제목 작성 전문가입니다.

플레이어의 플레이를 한 줄로 간결하게 표현하는 제목을 작성하세요.

[게임 결과 데이터]
- 최종 결과: {(survived ? "임무 완수" : "임무 실패")}
- 최종 안정성: {stability}/100
- 진행한 턴 수: {turnsPlayed}턴
- 마지막 선택: {_finalGameState.lastPlayerAction}
- 게임 배경: {_finalGameState.scene}
- 스토리 요약: {_finalGameState.plotSummary}
- 선택한 테마: {_finalGameState.selectedTheme}

[작성 지침]
- 5~10단어 이내의 짧고 강렬한 제목
- 플레이어의 선택과 결과를 함축적으로 표현
- 테마의 분위기 반영
- 성공/실패 여부보다는 '어떻게' 그 결과에 도달했는지 강조
- 예시:
  * 성공 (안정성 높음): '균형을 지킨 선택', '위기를 넘어선 판단'
  * 성공 (안정성 낮음): '아슬아슬한 생존', '희생 끝의 승리'
  * 실패: '무너진 신뢰', '연쇄된 오판', '통제 불능'

[주의사항]
- 따옴표, 큰따옴표 사용 금지
- 감탄사나 이모티콘 금지
- 단순히 '성공' 또는 '실패'라는 단어만 사용 금지
- 한글로 작성
- 제목만 출력 (설명이나 부연 설명 금지)";

        yield return StartCoroutine(CallGeminiAPI(prompt, (text) =>
        {
            callback?.Invoke(text?.Trim());
        }));
    }

    /// <summary>
    /// AI를 통해 최종 결산 요약 생성
    /// </summary>
    private IEnumerator GenerateAISummaryCoroutine(bool survived, System.Action<string> callback)
    {
        int stability = _finalGameState.stability.stability;
        int turnsPlayed = 14 - _finalGameState.turnsRemaining;

        string prompt = $@"당신은 게임 결산 스토리텔러입니다.

플레이어의 선택이 어떤 결과를 초래했는지, 그 여정을 서술적으로 작성하세요.

[게임 결과 데이터]
- 최종 결과: {(survived ? "임무 완수" : "임무 실패")}
- 최종 안정성: {stability}/100
- 진행한 턴 수: {turnsPlayed}턴
- 마지막 선택: {_finalGameState.lastPlayerAction}
- 게임 배경: {_finalGameState.scene}
- 스토리 요약: {_finalGameState.plotSummary}
- 선택한 테마: {_finalGameState.selectedTheme}

[작성 지침]
1. **선택의 여정**: 플레이어의 주요 선택들이 상황을 어떻게 변화시켰는지 서술 (2-3문장)
   - 게임 배경과 스토리 맥락 활용
   - 구체적인 선택과 그 영향 언급

2. **최종 결과**: 모든 선택이 초래한 최종 상황 설명 (2-3문장)
   - 성공 시: 어떤 선택들이 안정성을 유지하게 했는지
   - 실패 시: 어떤 선택들이 붕괴를 초래했는지
   - 안정성 수치({stability}/100)를 맥락에 녹여서 표현

3. **여운**: 플레이어의 결정이 남긴 의미 (1-2문장)
   - 도덕적/전략적 함의
   - 선택의 무게감 전달

[출력 형식]
당신의 선택
[선택의 여정 서술]

최종 결과
[최종 상황 설명]

[여운 메시지]

[주의사항]
- 등급 평가 절대 금지
- 플레이어의 구체적인 선택과 결과에 초점
- 게임 테마({_finalGameState.selectedTheme})의 분위기 반영
- 서술적이고 몰입감 있는 문체
- 한글로 작성
- 과도한 문학적 수사 지양, 명확하고 간결하게";

        yield return StartCoroutine(CallGeminiAPI(prompt, (text) =>
        {
            callback?.Invoke(text?.Trim());
        }));
    }

    /// <summary>
    /// Gemini API 호출
    /// </summary>
    private IEnumerator CallGeminiAPI(string prompt, System.Action<string> callback)
    {
        string url = $"{BaseUrl}{ModelName}:generateContent?key={apiKey.apiKey}";

        // JSON 문자열 이스케이프 처리
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
                callback?.Invoke(null);
            }
            else
            {
                string rawResponse = request.downloadHandler.text;
                string extractedText = ExtractTextFromApiResponse(rawResponse);
                callback?.Invoke(extractedText);
            }
        }
    }

    /// <summary>
    /// API 응답에서 텍스트 추출
    /// </summary>
    private string ExtractTextFromApiResponse(string apiResponse)
    {
        try
        {
            // GeminiManager에 정의된 DTO(GeminiApiResponse)를 사용하여 안전하게 파싱
            GeminiApiResponse response = JsonUtility.FromJson<GeminiApiResponse>(apiResponse);

            if (response != null && 
                response.candidates != null && response.candidates.Length > 0 &&
                response.candidates[0].content != null && 
                response.candidates[0].content.parts != null && response.candidates[0].content.parts.Length > 0)
            {
                return response.candidates[0].content.parts[0].text;
            }

            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"텍스트 추출 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSON 문자열 이스케이프
    /// </summary>
    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    /// <summary>
    /// 디테일한 최종 요약 생성
    /// </summary>
    private string GenerateDetailedSummary(bool survived)
    {
        System.Text.StringBuilder summary = new System.Text.StringBuilder();
        int stability = _finalGameState.stability.stability;

        // === 1. 선택의 여정 ===
        summary.AppendLine("<b>당신의 선택</b>");
        summary.AppendLine(GetChoiceJourney(survived, stability));
        summary.AppendLine();

        // === 2. 최종 결과 ===
        summary.AppendLine("<b>최종 결과</b>");
        summary.AppendLine(GetFinalOutcome(survived, stability));
        summary.AppendLine();

        // === 3. 여운 ===
        summary.AppendLine(GetAftermath(survived, stability));

        return summary.ToString();
    }

    /// <summary>
    /// 선택의 여정 서술
    /// </summary>
    private string GetChoiceJourney(bool survived, int stability)
    {
        int turnsPlayed = 14 - _finalGameState.turnsRemaining;
        string lastAction = _finalGameState.lastPlayerAction;

        if (survived && stability >= 70)
        {
            return $"{turnsPlayed}턴 동안 당신은 신중한 판단을 이어갔습니다. " +
                   $"'{lastAction}'으로 마무리된 일련의 선택들은 상황을 안정적으로 유지시켰고, " +
                   "위기의 순간마다 균형을 찾아냈습니다.";
        }
        else if (survived)
        {
            return $"{turnsPlayed}턴의 여정은 결코 순탄하지 않았습니다. " +
                   $"'{lastAction}'을(를) 포함한 여러 결정들이 때로는 위험을 감수해야 했지만, " +
                   "끝까지 포기하지 않은 선택이 간신히 임무 완수로 이어졌습니다.";
        }
        else
        {
            return $"{turnsPlayed}턴 동안 이어진 선택들이 점차 상황을 악화시켰습니다. " +
                   $"'{lastAction}'에 이르기까지 내린 결정들은 의도와 달리 " +
                   "안정성을 무너뜨리는 결과를 초래했습니다.";
        }
    }

    /// <summary>
    /// 최종 결과 설명
    /// </summary>
    private string GetFinalOutcome(bool survived, int stability)
    {
        string scene = _finalGameState.scene;
        
        if (survived && stability >= 70)
        {
            return $"{scene}의 상황은 안정을 되찾았습니다. " +
                   $"안정성 {stability}/100을 유지하며 임무를 완수했고, " +
                   "당신의 선택은 최악의 시나리오를 막아냈습니다.";
        }
        else if (survived && stability >= 50)
        {
            return $"{scene}은(는) 완전하지는 않지만 작동하고 있습니다. " +
                   $"안정성 {stability}/100은 위태로운 수치지만, " +
                   "적어도 붕괴는 막아냈습니다. 앞으로가 더 중요할 것입니다.";
        }
        else if (survived)
        {
            return $"{scene}은(는) 간신히 버티고 있습니다. " +
                   $"안정성 {stability}/100은 언제 무너져도 이상하지 않은 수준이지만, " +
                   "당신은 마지막 순간까지 선택을 멈추지 않았고, 그것이 임무 완수로 이어졌습니다.";
        }
        else
        {
            return $"{scene}의 안정성이 {stability}/100까지 무너지며 모든 것이 붕괴했습니다. " +
                   "당신의 선택들은 연쇄적으로 상황을 악화시켰고, " +
                   "결국 돌이킬 수 없는 지점을 넘어서고 말았습니다.";
        }
    }

    /// <summary>
    /// 여운 메시지
    /// </summary>
    private string GetAftermath(bool survived, int stability)
    {
        if (survived && stability >= 70)
        {
            return "<i>때로는 올바른 선택이 어려운 선택입니다. 당신은 그 무게를 견뎌냈습니다.</i>";
        }
        else if (survived)
        {
            return "<i>생존은 성공의 다른 이름일까요, 아니면 단순한 행운일까요? 그 답은 당신의 다음 선택에 달려 있습니다.</i>";
        }
        else
        {
            return "<i>모든 선택에는 대가가 따릅니다. 때로는 그 대가가 우리가 감당할 수 있는 것보다 클 때가 있습니다.</i>";
        }
    }
}
