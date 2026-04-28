using UnityEditor;

namespace ParrelSync
{
    [InitializeOnLoad]
    public class EditorQuit
    {
        /// <summary>
        /// Is editor being closed
        /// </summary>
        public static bool IsQuiting { get; private set; }

        private static void Quit()
        {
            IsQuiting = true;
        }

        static EditorQuit()
        {
            IsQuiting = false;
            EditorApplication.quitting += Quit;
        }
    }
}
