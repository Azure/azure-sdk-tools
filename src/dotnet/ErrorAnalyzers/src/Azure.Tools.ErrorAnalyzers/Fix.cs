namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Base class for all fix instructions.
    /// </summary>
    public abstract class Fix
    {
        protected Fix(FixAction action)
        {
            Action = action;
        }

        /// <summary>
        /// What kind of fix is this?
        /// </summary>
        public FixAction Action { get; }
    }
}
