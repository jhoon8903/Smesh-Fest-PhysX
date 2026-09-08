using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Framework.Log
{
    public static class GameLog
    {
        public enum GameLogLevel
        {
            Debug, Warning, Error
        }

        private static GameLogLevel _currentLogLevel = GameLogLevel.Debug;

        public static void SetLogLevel(GameLogLevel level)
        {
            _currentLogLevel = level;
        }
        [Conditional("USE_GAMELOG")]
        public static void D(string log)
        {
            if (_currentLogLevel >= GameLogLevel.Debug)
            {
                Debug.Log($"<color=#3498db>[INFO]</color>: {log}");
            }
        }
        
        [Conditional("USE_GAMELOG")]
        public static void D(string log, params object[] args)
        {
            if (_currentLogLevel >= GameLogLevel.Debug)
            {
                Debug.LogFormat($"<color=#3498db>[INFO]</color>: {log}", args);
            }
        }
        
        [Conditional("USE_GAMELOG")]
        public static void W(string log)
        {
            if (_currentLogLevel >= GameLogLevel.Warning)
            {
                Debug.LogWarning($"<color=color=#f39c12>[WARNING]</color>: {log}");
            }
        }
        
        [Conditional("USE_GAMELOG")]
        public static void W(string log, params object[] args)
        {
            if (_currentLogLevel >= GameLogLevel.Warning)
            {
                Debug.LogWarningFormat($"<color=color=#f39c12>[WARNING]</color>: {log}", args);
            }
        }
        
        [Conditional("USE_GAMELOG")]
        public static void E(string log)
        {
            if (_currentLogLevel >= GameLogLevel.Error)
            {
                Debug.LogError($"<color=#e74c3c>[ERROR]<color>: {log}");
            }
        }
        
        [Conditional("USE_GAMELOG")]
        public static void E(string log, params object[] args)
        {
            if (_currentLogLevel >= GameLogLevel.Error)
            {
                Debug.LogErrorFormat($"<color=#e74c3c>[ERROR]<color>: {log}", args);
            }
        }
    }
}