namespace BabbdiCoop
{
    /// <summary>v0.10.44: the ONE turn rule of Zmejka, shared by the solo cabinet
    /// (`ArcadeMiniGame`) and the duel cabinet (`ArcadeDuelGame`).
    ///
    /// Why this file exists: the same 180-degree bug shipped twice because the rule was
    /// copy-pasted into two classes. A player pressed right -> up -> left inside one move
    /// tick; `left` was validated against the *queued* `up` instead of the direction the
    /// head actually moved in, was accepted, and the next step put the head into its own
    /// neck. Both copies are now this single function, and `Tests/SnakeTurnTests.cs`
    /// asserts the behaviour on every build.
    ///
    /// Terms:
    ///   dx, dy   - the direction the head MOVED in the last step (never a queued turn)
    ///   qx, qy   - the single turn buffered for the next step
    ///   has      - true when a buffered turn is waiting
    ///   wx, wy   - the requested direction
    /// </summary>
    internal static class SnakeTurn
    {
        /// <summary>Validates a requested turn.
        /// Returns true when the request became the buffered turn. A request equal to the
        /// current direction cancels the buffer (the player straightened out) and returns
        /// false; a request opposite to the current direction is ignored and returns false.
        /// </summary>
        public static bool Accept(int dx, int dy, ref int qx, ref int qy, ref bool has, int wx, int wy)
        {
            if (wx == dx && wy == dy)
            {
                has = false;
                return false;
            }
            if (wx == -dx && wy == -dy)
            {
                // a real 180 inside one cell - the only turn that may never happen
                return false;
            }
            qx = wx;
            qy = wy;
            has = true;
            return true;
        }
    }
}
