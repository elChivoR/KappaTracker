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
    internal static class BootstrapPatch
    {
        public static IEnumerator Run()
        {
            while (string.IsNullOrEmpty(RequestHandler.SessionId))
                yield return new WaitForSeconds(1f);

            MilestoneClient.Fetch();
        }
    }
}
