using System;
using System.Linq;

using Dalamud.Game;
using Serilog;

using static Dalamud.Game.Framework;

namespace Dalamud.Utility
{
    /// <summary>
    /// Extensions for Events.
    /// </summary>
    internal static class EventHandlerExtensions
    {
        /// <summary>
        /// Replacement for Invoke() on EventHandlers to catch exceptions that stop event propagation in case
        /// of a thrown Exception inside of an invocation.
        /// </summary>
        /// <param name="eh">The EventHandler in question.</param>
        /// <param name="sender">Default sender for Invoke equivalent.</param>
        /// <param name="e">Default EventArgs for Invoke equivalent.</param>
        public static void InvokeSafely(this EventHandler eh, object sender, EventArgs e)
        {
            if (eh == null)
                return;

            foreach (var handler in eh.GetInvocationList().Cast<EventHandler>())
            {
                HandleInvoke(() => handler(sender, e));
            }
        }

        /// <summary>
        /// Replacement for Invoke() on generic EventHandlers to catch exceptions that stop event propagation in case
        /// of a thrown Exception inside of an invocation.
        /// </summary>
        /// <param name="eh">The EventHandler in question.</param>
        /// <param name="sender">Default sender for Invoke equivalent.</param>
        /// <param name="e">Default EventArgs for Invoke equivalent.</param>
        /// <typeparam name="T">Type of EventArgs.</typeparam>
        public static void InvokeSafely<T>(this EventHandler<T> eh, object sender, T e)
        {
            if (eh == null)
                return;

            foreach (var handler in eh.GetInvocationList().Cast<EventHandler<T>>())
            {
                HandleInvoke(() => handler(sender, e));
            }
        }

        /// <summary>
        /// Replacement for Invoke() on event Actions to catch exceptions that stop event propagation in case
        /// of a thrown Exception inside of an invocation.
        /// </summary>
        /// <param name="act">The Action in question.</param>
        public static void InvokeSafely(this Action act)
        {
            if (act == null)
                return;

            foreach (var action in act.GetInvocationList().Cast<Action>())
            {
                HandleInvoke(action);
            }
        }

        /// <summary>
        /// Replacement for Invoke() on OnUpdateDelegate to catch exceptions that stop event propagation in case
        /// of a thrown Exception inside of an invocation.
        /// </summary>
        /// <param name="updateDelegate">The OnUpdateDelegate in question.</param>
        /// <param name="framework">Framework to be passed on to OnUpdateDelegate.</param>
        public static void InvokeSafely(this OnUpdateDelegate updateDelegate, Framework framework)
        {
            if (updateDelegate == null)
                return;

            foreach (var action in updateDelegate.GetInvocationList().Cast<OnUpdateDelegate>())
            {
                HandleInvoke(() => action(framework));
            }
        }

        private static void HandleInvoke(Action act)
        {
            try
            {
                act();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during raise of {handler}", act.Method);
            }
        }
    }
}
