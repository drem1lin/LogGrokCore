using LogGrokCore.Colors.Configuration;
using LogGrokCore.Data;

namespace LogGrokCore
{
    /// <summary>
    /// A named set of parsing formats and highlighting rules. Profiles are selected globally in
    /// the legacy UI, so every open document is parsed with the same independent configuration.
    /// </summary>
    public sealed class ProfileSettings
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Set to false when omitted sections must stay empty instead of inheriting the legacy
        /// top-level settings. Null preserves compatibility with profiles created by older builds.
        /// </summary>
        public bool? InheritLegacySettings { get; set; }

        /// <summary>Null means "use the legacy top-level value" for compatibility.</summary>
        public ColorSettings? ColorSettings { get; set; }

        /// <summary>Null means "use the legacy top-level value" for compatibility.</summary>
        public LogFormat[]? LogFormats { get; set; }
    }
}
