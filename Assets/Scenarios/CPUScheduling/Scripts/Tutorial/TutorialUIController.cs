using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUIController : MonoBehaviour
{
    public static TutorialUIController Instance { get; private set; }

    [Header("Main Panels")]
    [SerializeField] private GameObject tutorialRootPanel;
    [SerializeField] private GameObject conceptViewPanel;
    [SerializeField] private GameObject algorithmViewPanel;

    [Header("Navigation & Tabs")]
    [SerializeField] private Button conceptsTabButton;
    [SerializeField] private Button algorithmsTabButton;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Text pageIndicatorText;
    [SerializeField] private Transform lessonListContainer;
    [SerializeField] private GameObject lessonButtonPrefab;

    [Header("Concept Display Elements")]
    [SerializeField] private Text conceptTitleText;
    [SerializeField] private Text conceptCategoryText;
    [SerializeField] private Text conceptExplanationText;
    [SerializeField] private Text conceptRuleText;
    [SerializeField] private Text conceptExampleText;
    [SerializeField] private Text conceptVisualText;

    [Header("Algorithm Display Elements")]
    [SerializeField] private Text algoNameText;
    [SerializeField] private Text algoDefinitionText;
    [SerializeField] private Text algoSelectionText;
    [SerializeField] private Text algoPreemptionBadge;
    [SerializeField] private Text algoAdvantagesText;
    [SerializeField] private Text algoDisadvantagesText;
    [SerializeField] private Text algoStarvationText;
    [SerializeField] private Text algoUseCaseText;

    [Header("Interactive Challenge / Quiz Section")]
    [SerializeField] private Text quizQuestionText;
    [SerializeField] private Text quizScenarioText;
    [SerializeField] private Transform quizOptionsContainer;
    [SerializeField] private GameObject quizOptionButtonPrefab;
    [SerializeField] private GameObject quizFeedbackCard;
    [SerializeField] private Image quizFeedbackBg;
    [SerializeField] private Text quizFeedbackTitle;
    [SerializeField] private Text quizFeedbackText;

    // State
    private bool isConceptMode = true;
    private int currentConceptIndex = 0;
    private int currentAlgoIndex = 0;
    private QuizChallenge activeChallenge = null;

    private List<GameObject> optionButtonPool = new List<GameObject>();
    private List<GameObject> sidebarButtonPool = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        if (conceptsTabButton != null) conceptsTabButton.onClick.AddListener(() => SwitchTab(true));
        if (algorithmsTabButton != null) algorithmsTabButton.onClick.AddListener(() => SwitchTab(false));
        if (prevButton != null) prevButton.onClick.AddListener(PreviousLesson);
        if (nextButton != null) nextButton.onClick.AddListener(NextLesson);

        SwitchTab(true);
    }

    public void OpenTutorial()
    {
        if (tutorialRootPanel != null)
        {
            tutorialRootPanel.SetActive(true);
            RenderCurrentLesson();
        }
    }

    public void CloseTutorial()
    {
        if (tutorialRootPanel != null)
        {
            tutorialRootPanel.SetActive(false);
        }
    }

    public void SwitchTab(bool toConcepts)
    {
        isConceptMode = toConcepts;

        if (conceptsTabButton != null)
        {
            var colors = conceptsTabButton.colors;
            colors.normalColor = isConceptMode ? new Color(0.20f, 0.55f, 0.95f) : new Color(0.12f, 0.16f, 0.22f);
            conceptsTabButton.colors = colors;
        }

        if (algorithmsTabButton != null)
        {
            var colors = algorithmsTabButton.colors;
            colors.normalColor = !isConceptMode ? new Color(0.20f, 0.55f, 0.95f) : new Color(0.12f, 0.16f, 0.22f);
            algorithmsTabButton.colors = colors;
        }

        if (conceptViewPanel != null) conceptViewPanel.SetActive(isConceptMode);
        if (algorithmViewPanel != null) algorithmViewPanel.SetActive(!isConceptMode);

        RebuildSidebar();
        RenderCurrentLesson();
    }

    public void SelectConcept(int index)
    {
        var list = TutorialDatabase.GetConcepts();
        if (index >= 0 && index < list.Count)
        {
            currentConceptIndex = index;
            isConceptMode = true;
            if (conceptViewPanel != null) conceptViewPanel.SetActive(true);
            if (algorithmViewPanel != null) algorithmViewPanel.SetActive(false);
            RenderCurrentLesson();
        }
    }

    public void SelectAlgorithmLesson(int index)
    {
        var list = TutorialDatabase.GetAlgorithms();
        if (index >= 0 && index < list.Count)
        {
            currentAlgoIndex = index;
            isConceptMode = false;
            if (conceptViewPanel != null) conceptViewPanel.SetActive(false);
            if (algorithmViewPanel != null) algorithmViewPanel.SetActive(true);
            RenderCurrentLesson();
        }
    }

    public void NextLesson()
    {
        if (isConceptMode)
        {
            var list = TutorialDatabase.GetConcepts();
            if (currentConceptIndex < list.Count - 1)
            {
                currentConceptIndex++;
            }
            else
            {
                // Transition smoothly into Algorithm lessons
                isConceptMode = false;
                currentAlgoIndex = 0;
                SwitchTab(false);
                return;
            }
        }
        else
        {
            var list = TutorialDatabase.GetAlgorithms();
            if (currentAlgoIndex < list.Count - 1)
            {
                currentAlgoIndex++;
            }
        }
        RenderCurrentLesson();
    }

    public void PreviousLesson()
    {
        if (isConceptMode)
        {
            if (currentConceptIndex > 0)
            {
                currentConceptIndex--;
            }
        }
        else
        {
            if (currentAlgoIndex > 0)
            {
                currentAlgoIndex--;
            }
            else
            {
                // Transition back into Concept lessons
                isConceptMode = true;
                currentConceptIndex = TutorialDatabase.GetConcepts().Count - 1;
                SwitchTab(true);
                return;
            }
        }
        RenderCurrentLesson();
    }

    private void RebuildSidebar()
    {
        if (lessonListContainer == null || lessonButtonPrefab == null) return;

        int totalCount = isConceptMode
            ? TutorialDatabase.GetConcepts().Count
            : TutorialDatabase.GetAlgorithms().Count;

        EnsurePool(sidebarButtonPool, lessonButtonPrefab, lessonListContainer, totalCount);

        if (isConceptMode)
        {
            var concepts = TutorialDatabase.GetConcepts();
            for (int i = 0; i < concepts.Count; i++)
            {
                int idx = i;
                var btnObj = sidebarButtonPool[i];
                btnObj.SetActive(true);

                var txt = btnObj.GetComponentInChildren<Text>();
                if (txt != null) txt.text = concepts[i].Title;

                var btn = btnObj.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    currentConceptIndex = idx;
                    RenderCurrentLesson();
                });

                var img = btnObj.GetComponent<Image>();
                if (img != null)
                {
                    img.color = (i == currentConceptIndex) ? new Color(0.20f, 0.45f, 0.85f) : new Color(0.12f, 0.16f, 0.24f);
                }
            }
        }
        else
        {
            var algos = TutorialDatabase.GetAlgorithms();
            for (int i = 0; i < algos.Count; i++)
            {
                int idx = i;
                var btnObj = sidebarButtonPool[i];
                btnObj.SetActive(true);

                var txt = btnObj.GetComponentInChildren<Text>();
                if (txt != null) txt.text = algos[i].Name;

                var btn = btnObj.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    currentAlgoIndex = idx;
                    RenderCurrentLesson();
                });

                var img = btnObj.GetComponent<Image>();
                if (img != null)
                {
                    img.color = (i == currentAlgoIndex) ? new Color(0.20f, 0.45f, 0.85f) : new Color(0.12f, 0.16f, 0.24f);
                }
            }
        }

        for (int i = totalCount; i < sidebarButtonPool.Count; i++)
        {
            sidebarButtonPool[i].SetActive(false);
        }
    }

    public void RenderCurrentLesson()
    {
        if (quizFeedbackCard != null) quizFeedbackCard.SetActive(false);

        if (isConceptMode)
        {
            var concepts = TutorialDatabase.GetConcepts();
            if (concepts.Count == 0) return;
            currentConceptIndex = Mathf.Clamp(currentConceptIndex, 0, concepts.Count - 1);
            var c = concepts[currentConceptIndex];

            if (conceptTitleText != null) conceptTitleText.text = c.Title.ToUpper();
            if (conceptCategoryText != null) conceptCategoryText.text = $"CATEGORY: {c.Category.ToUpper()}";
            if (conceptExplanationText != null) conceptExplanationText.text = c.ShortExplanation;
            if (conceptRuleText != null) conceptRuleText.text = $"KEY RULE / FORMULA:\n{c.FormulaOrRule}";
            if (conceptExampleText != null) conceptExampleText.text = $"PRACTICAL EXAMPLE:\n{c.Example}";
            if (conceptVisualText != null) conceptVisualText.text = c.VisualKey;

            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"CONCEPT {currentConceptIndex + 1} OF {concepts.Count}";
            }

            activeChallenge = c.Challenge;
        }
        else
        {
            var algos = TutorialDatabase.GetAlgorithms();
            if (algos.Count == 0) return;
            currentAlgoIndex = Mathf.Clamp(currentAlgoIndex, 0, algos.Count - 1);
            var a = algos[currentAlgoIndex];

            if (algoNameText != null) algoNameText.text = a.Name.ToUpper();
            if (algoDefinitionText != null) algoDefinitionText.text = a.Definition;
            if (algoSelectionText != null) algoSelectionText.text = $"HOW IT SELECTS NEXT PROCESS:\n{a.SelectionMechanism}";
            if (algoPreemptionBadge != null)
            {
                algoPreemptionBadge.text = a.IsPreemptive ? "PREEMPTIVE: YES" : "PREEMPTIVE: NO (Non-preemptive)";
                algoPreemptionBadge.color = a.IsPreemptive ? new Color(0.2f, 0.85f, 0.5f) : new Color(1.0f, 0.7f, 0.2f);
            }
            if (algoAdvantagesText != null) algoAdvantagesText.text = $"ADVANTAGES:\n{a.Advantages}";
            if (algoDisadvantagesText != null) algoDisadvantagesText.text = $"DISADVANTAGES:\n{a.Disadvantages}";
            if (algoStarvationText != null) algoStarvationText.text = $"STARVATION POSSIBILITY:\n{a.StarvationRisk}";
            if (algoUseCaseText != null) algoUseCaseText.text = $"TYPICAL USE CASE:\n{a.TypicalUseCase}";

            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"ALGORITHM {currentAlgoIndex + 1} OF {algos.Count}";
            }

            activeChallenge = a.Challenge;
        }

        RenderQuizChallenge(activeChallenge);
        HighlightCurrentSidebarItem();
    }

    private void HighlightCurrentSidebarItem()
    {
        int activeIdx = isConceptMode ? currentConceptIndex : currentAlgoIndex;
        int max = isConceptMode ? TutorialDatabase.GetConcepts().Count : TutorialDatabase.GetAlgorithms().Count;

        for (int i = 0; i < max && i < sidebarButtonPool.Count; i++)
        {
            var img = sidebarButtonPool[i].GetComponent<Image>();
            if (img != null)
            {
                img.color = (i == activeIdx) ? new Color(0.20f, 0.45f, 0.85f) : new Color(0.12f, 0.16f, 0.24f);
            }
        }
    }

    private void RenderQuizChallenge(QuizChallenge challenge)
    {
        if (challenge == null || quizOptionsContainer == null || quizOptionButtonPrefab == null) return;

        if (quizQuestionText != null) quizQuestionText.text = $"QUESTION: {challenge.Question}";
        if (quizScenarioText != null) quizScenarioText.text = challenge.ScenarioDetails;

        EnsurePool(optionButtonPool, quizOptionButtonPrefab, quizOptionsContainer, challenge.Options.Count);

        for (int i = 0; i < challenge.Options.Count; i++)
        {
            var opt = challenge.Options[i];
            var btnObj = optionButtonPool[i];
            btnObj.SetActive(true);

            var txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null) txt.text = opt.OptionText;

            var img = btnObj.GetComponent<Image>();
            if (img != null) img.color = new Color(0.15f, 0.22f, 0.32f);

            var btn = btnObj.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnOptionSelected(opt));
        }

        for (int i = challenge.Options.Count; i < optionButtonPool.Count; i++)
        {
            optionButtonPool[i].SetActive(false);
        }
    }

    private void OnOptionSelected(QuizOption option)
    {
        if (quizFeedbackCard == null) return;

        quizFeedbackCard.SetActive(true);

        if (option.IsCorrect)
        {
            if (quizFeedbackBg != null) quizFeedbackBg.color = new Color(0.12f, 0.40f, 0.22f, 0.98f);
            if (quizFeedbackTitle != null)
            {
                quizFeedbackTitle.text = "CORRECT ANSWER! 🎉";
                quizFeedbackTitle.color = new Color(0.35f, 0.95f, 0.5f);
            }
            if (quizFeedbackText != null)
            {
                quizFeedbackText.text = option.Explanation;
            }
        }
        else
        {
            if (quizFeedbackBg != null) quizFeedbackBg.color = new Color(0.45f, 0.15f, 0.18f, 0.98f);
            if (quizFeedbackTitle != null)
            {
                quizFeedbackTitle.text = "NOT QUITE — TRY AGAIN ❌";
                quizFeedbackTitle.color = new Color(1.0f, 0.45f, 0.45f);
            }
            if (quizFeedbackText != null)
            {
                quizFeedbackText.text = option.Explanation;
            }
        }
    }

    private void EnsurePool(List<GameObject> pool, GameObject prefab, Transform parent, int required)
    {
        if (prefab == null || parent == null) return;
        while (pool.Count < required)
        {
            var obj = Instantiate(prefab, parent);
            obj.SetActive(false);
            pool.Add(obj);
        }
    }
}
