using System;
using UnityEngine;
using VContainer;

namespace InGame.Level
{
    /// <summary>
    /// Owns the deliberate handoff from authored Blocks to one rented level. It never starts by
    /// itself: GameFlow or another caller must explicitly request TryStart.
    /// </summary>
    public sealed class LevelSession : MonoBehaviour
    {
        [SerializeField] private Transform authoredBlocksRoot;

        private LevelSpawner levelSpawner;
        private bool started;
        private bool authoredBlocksWereActive;

        public bool IsStarted => started;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(LevelSpawner injectedLevelSpawner)
        {
            if (levelSpawner != null)
                throw new InvalidOperationException("LevelSession was already configured.");
            levelSpawner = injectedLevelSpawner ?? throw new ArgumentNullException(nameof(injectedLevelSpawner));
        }

        /// <summary>
        /// Hides authored Blocks only for the attempted runtime handoff. A failed spawn restores
        /// their exact prior activeSelf state; a successful one intentionally leaves them hidden.
        /// </summary>
        public bool TryStart(out string failure)
        {
            failure = null;
            if (started)
            {
                failure = "LevelSession is already started. Call ReturnAll before starting again.";
                return false;
            }
            if (levelSpawner == null)
            {
                failure = "LevelSession requires LevelSpawner injection before starting.";
                return false;
            }
            if (authoredBlocksRoot == null)
            {
                failure = "LevelSession requires an explicit authored Blocks root.";
                return false;
            }

            authoredBlocksWereActive = authoredBlocksRoot.gameObject.activeSelf;
            authoredBlocksRoot.gameObject.SetActive(false);
            try
            {
                if (levelSpawner.TrySpawn(out failure))
                {
                    started = true;
                    return true;
                }
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }

            authoredBlocksRoot.gameObject.SetActive(authoredBlocksWereActive);
            return false;
        }

        /// <summary>Explicitly returns the runtime level and restores the authored Blocks state captured at start.</summary>
        public void ReturnAll()
        {
            if (levelSpawner == null)
                throw new InvalidOperationException("LevelSession requires LevelSpawner injection before returning.");

            try
            {
                levelSpawner.ReturnAll();
            }
            finally
            {
                RestoreAuthoredBlocks();
                started = false;
            }
        }

        /// <summary>Explicit reset alias for callers that own a level restart command.</summary>
        public void ResetSession() => ReturnAll();

        private void RestoreAuthoredBlocks()
        {
            if (authoredBlocksRoot == null)
                throw new InvalidOperationException("LevelSession lost its authored Blocks root before returning.");
            if (started)
                authoredBlocksRoot.gameObject.SetActive(authoredBlocksWereActive);
        }
    }
}
