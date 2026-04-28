using UnityEditor;
using UnityEngine;

namespace ParrelSync.NonCore
{
    public class OtherMenuItem
    {
        [MenuItem("Tools/ParrelSync/GitHub/View this project on GitHub", priority = 10)]
        private static void OpenGitHub()
        {
            Application.OpenURL(ExternalLinks.GitHubHome);
        }

        [MenuItem("Tools/ParrelSync/GitHub/View FAQ", priority = 11)]
        private static void OpenFaq()
        {
            Application.OpenURL(ExternalLinks.Faq);
        }

        [MenuItem("Tools/ParrelSync/GitHub/View Issues", priority = 12)]
        private static void OpenGitHubIssues()
        {
            Application.OpenURL(ExternalLinks.GitHubIssue);
        }
    }
}
