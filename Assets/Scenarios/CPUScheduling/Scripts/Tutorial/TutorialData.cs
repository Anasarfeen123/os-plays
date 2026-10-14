using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class QuizOption
{
    public string OptionText;
    public bool IsCorrect;
    public string Explanation;

    public QuizOption(string text, bool isCorrect, string explanation)
    {
        OptionText = text;
        IsCorrect = isCorrect;
        Explanation = explanation;
    }
}

[System.Serializable]
public class QuizChallenge
{
    public string Question;
    public string ScenarioDetails; // e.g. "P1 (Arr=0, Bst=5), P2 (Arr=1, Bst=2), P3 (Arr=2, Bst=8)"
    public List<QuizOption> Options = new List<QuizOption>();
    public string GeneralExplanation;

    public QuizChallenge(string question, string scenario, List<QuizOption> options, string generalExplanation)
    {
        Question = question;
        ScenarioDetails = scenario;
        Options = options;
        GeneralExplanation = generalExplanation;
    }
}

[System.Serializable]
public class ConceptLesson
{
    public string Id;
    public string Title;
    public string Category; // "Fundamental Concept"
    public string ShortExplanation;
    public string FormulaOrRule;
    public string Example;
    public string VisualKey; // Visual hint or badge format
    public QuizChallenge Challenge;
}

[System.Serializable]
public class AlgorithmLesson
{
    public string Id;
    public string Name;
    public string Definition;
    public string SelectionMechanism;
    public bool IsPreemptive;
    public string Advantages;
    public string Disadvantages;
    public string StarvationRisk;
    public string TypicalUseCase;
    public QuizChallenge Challenge;
}
