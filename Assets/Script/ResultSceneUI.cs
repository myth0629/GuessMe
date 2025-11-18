using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 게임 결산 씬 UI 관리
/// </summary>
public class ResultSceneUI : MonoBehaviour
{
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

    [Header("세력 신뢰도")]
    [Tooltip("세력 1 이름")]
    public TextMeshProUGUI faction1NameText;
    [Tooltip("세력 1 신뢰도 슬라이더")]
    public Slider faction1Slider;
    [Tooltip("세력 1 값 텍스트")]
    public TextMeshProUGUI faction1ValueText;

    [Tooltip("세력 2 이름")]
    public TextMeshProUGUI faction2NameText;
    [Tooltip("세력 2 신뢰도 슬라이더")]
    public Slider faction2Slider;
    [Tooltip("세력 2 값 텍스트")]
    public TextMeshProUGUI faction2ValueText;

    [Tooltip("세력 3 이름")]
    public TextMeshProUGUI faction3NameText;
    [Tooltip("세력 3 신뢰도 슬라이더")]
    public Slider faction3Slider;
    [Tooltip("세력 3 값 텍스트")]
    public TextMeshProUGUI faction3ValueText;

    [Header("자원")]
    [Tooltip("최종 식량")]
    public TextMeshProUGUI finalFoodText;

    [Header("버튼")]
    [Tooltip("메인 메뉴로 버튼 (StartSceneUI 컴포넌트 사용)")]
    public StartSceneUI sceneNavigator;

    private GameState _finalGameState;

    private void Start()
    {
        // PlayerPrefs에서 최종 게임 상태 로드
        string gameStateJson = PlayerPrefs.GetString("FinalGameState", "");
        
        if (string.IsNullOrEmpty(gameStateJson))
        {
            Debug.LogError("최종 게임 상태를 찾을 수 없습니다!");
            if (summaryText != null)
                summaryText.text = "게임 상태를 불러올 수 없습니다.";
            return;
        }

        _finalGameState = JsonUtility.FromJson<GameState>(gameStateJson);
        DisplayResults();
    }

    /// <summary>
    /// 최종 결과 표시
    /// </summary>
    private void DisplayResults()
    {
        if (_finalGameState == null) return;

        // 생존 성공 여부 판단
        bool survived = _finalGameState.stability.stability >= 30 && _finalGameState.resources.food > 0;

        // 제목 설정
        if (resultTitleText != null)
        {
            resultTitleText.text = survived ? "7일 생존 성공!" : "생존 실패...";
            resultTitleText.color = survived ? Color.green : Color.red;
        }

        // 요약 텍스트
        if (summaryText != null)
        {
            summaryText.text = $"{_finalGameState.plotSummary}\n\n" +
                              $"당신은 7일간의 위기 상황을 {(survived ? "성공적으로 극복했습니다" : "극복하지 못했습니다")}.";
        }

        // 안정도
        if (finalStabilitySlider != null)
        {
            finalStabilitySlider.value = _finalGameState.stability.stability;
        }
        if (stabilityValueText != null)
        {
            stabilityValueText.text = $"{_finalGameState.stability.stability}/100";
        }

        // 세력 신뢰도
        if (_finalGameState.factionTrust != null && _finalGameState.factionTrust.factions != null)
        {
            int factionCount = Mathf.Min(_finalGameState.factionTrust.factions.Length, 3);

            // 세력 1
            if (factionCount > 0)
            {
                if (faction1NameText != null)
                    faction1NameText.text = _finalGameState.factionTrust.factions[0].name;
                if (faction1Slider != null)
                    faction1Slider.value = _finalGameState.factionTrust.factions[0].trust;
                if (faction1ValueText != null)
                    faction1ValueText.text = $"{_finalGameState.factionTrust.factions[0].trust}/100";

                SetFactionUIActive(1, true);
            }
            else
            {
                SetFactionUIActive(1, false);
            }

            // 세력 2
            if (factionCount > 1)
            {
                if (faction2NameText != null)
                    faction2NameText.text = _finalGameState.factionTrust.factions[1].name;
                if (faction2Slider != null)
                    faction2Slider.value = _finalGameState.factionTrust.factions[1].trust;
                if (faction2ValueText != null)
                    faction2ValueText.text = $"{_finalGameState.factionTrust.factions[1].trust}/100";

                SetFactionUIActive(2, true);
            }
            else
            {
                SetFactionUIActive(2, false);
            }

            // 세력 3
            if (factionCount > 2)
            {
                if (faction3NameText != null)
                    faction3NameText.text = _finalGameState.factionTrust.factions[2].name;
                if (faction3Slider != null)
                    faction3Slider.value = _finalGameState.factionTrust.factions[2].trust;
                if (faction3ValueText != null)
                    faction3ValueText.text = $"{_finalGameState.factionTrust.factions[2].trust}/100";

                SetFactionUIActive(3, true);
            }
            else
            {
                SetFactionUIActive(3, false);
            }
        }

        // 최종 자원
        if (finalFoodText != null)
        {
            float daysOfFood = _finalGameState.resources.food / 3f;
            finalFoodText.text = $"식량: {_finalGameState.resources.food} ({daysOfFood:F1}일치)";
        }
    }

    /// <summary>
    /// 세력 UI 표시/숨김
    /// </summary>
    private void SetFactionUIActive(int factionNumber, bool active)
    {
        switch (factionNumber)
        {
            case 1:
                if (faction1NameText != null) faction1NameText.gameObject.SetActive(active);
                if (faction1Slider != null) faction1Slider.gameObject.SetActive(active);
                if (faction1ValueText != null) faction1ValueText.gameObject.SetActive(active);
                break;
            case 2:
                if (faction2NameText != null) faction2NameText.gameObject.SetActive(active);
                if (faction2Slider != null) faction2Slider.gameObject.SetActive(active);
                if (faction2ValueText != null) faction2ValueText.gameObject.SetActive(active);
                break;
            case 3:
                if (faction3NameText != null) faction3NameText.gameObject.SetActive(active);
                if (faction3Slider != null) faction3Slider.gameObject.SetActive(active);
                if (faction3ValueText != null) faction3ValueText.gameObject.SetActive(active);
                break;
        }
    }
}
