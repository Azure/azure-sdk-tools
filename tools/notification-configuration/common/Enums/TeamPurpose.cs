namespace Azure.Sdk.Tools.NotificationConfiguration.Enums
{
    /// <summary>
    /// Purpose of a DevOps team
    /// </summary>
    public enum TeamPurpose
    {
        /// <summary>
        /// A team assigned to receive notification for build failures. This
        /// team can be manually managed.
        /// </summary>
        /// <remarks>
        /// Explicitly set value because YAML serialization does not emit 
        /// default values (i.e. 0)
        /// </remarks>
        ParentNotificationTeam = 1,

        /// <summary>
        /// A team that lives within a ParentNotificationTeam, its members are
        /// synchronized from external data sources. Automation may overwrite
        /// changes made to this group.
        /// </summary>
        /// <remarks>
        /// Explicitly set value because YAML serialization does not emit 
        /// default values (i.e. 0)
        /// </remarks>
        SynchronizedNotificationTeam = 2,
    }
}
