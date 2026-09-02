using System.Collections;
using SPT.Common.Http;
using UnityEngine;

namespace KappaTracker.Client
{
    /// <summary>
    /// Runs <see cref="MilestoneClient.Fetch"/> exactly once per game session, after the
    /// backend is reachable.
    ///
    /// A Harmony hook was the first idea (patch the method that raises the main menu after
    /// login), but no clearly-reliable one-shot target survived inspection:
    /// <c>EFT.UI.MenuScreen.Show</c> is overloaded (1-arg override + 3-arg impl), fires on
    /// every return to the menu, and its invocation path runs through un-inspected
    /// <c>EftScreen</c> internals. Instead this is a one-second Unity coroutine poll:
    /// <c>RequestHandler.SessionId</c> is populated from the <c>-token=</c> launch arg at
    /// process start and the SPT server is always up before the game, so a single
    /// <see cref="MilestoneClient.Fetch"/> once the token is visible is reliable.
    /// <see cref="MilestoneClient.Fetch"/> is itself idempotent (early-returns when
    /// <see cref="KappaTagService.Ready"/>), so a repeat call is harmless.
    /// </summary>
    internal static class MilestoneBootstrap
    {
        /// <summary>
        /// ~2 minutes of polling at a 1 s cadence. <c>RequestHandler.SessionId</c> is set
        /// from the <c>-token=</c> launch arg very early in startup, so this ceiling is only
        /// ever reached on a broken launch (no token) — in which case we give up quietly
        /// rather than poll forever.
        /// </summary>
        private const int MaxAttempts = 120;

        public static IEnumerator Run()
        {
            var attempts = 0;
            while (string.IsNullOrEmpty(RequestHandler.SessionId))
            {
                if (++attempts > MaxAttempts)
                {
                    Plugin.Log.LogWarning(
                        "KappaTracker: session token never appeared after ~2 min; milestone fetch " +
                        "skipped, tags disabled this session.");
                    yield break;
                }

                // Realtime wait: the loading screen pins Time.timeScale to 0, which would
                // freeze a plain WaitForSeconds indefinitely.
                yield return new WaitForSecondsRealtime(1f);
            }

            MilestoneClient.Fetch();
        }
    }
}
