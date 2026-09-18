namespace Cascade.Bootstrap
{
    /// <summary>
    /// Vocabulary for build/stage classification. The mapping to concrete policy
    /// (log level, player-side resolution, resource options) belongs to the starter subclass —
    /// see <c>MobileBootstrapEntry</c> / <c>IndieBootstrapEntry</c>.
    /// </summary>
    public enum BootstrapEnvironment
    {
        Dev,
        Beta,
        Gold
    }
}
