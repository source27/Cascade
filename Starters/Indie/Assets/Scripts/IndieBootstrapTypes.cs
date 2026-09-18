namespace Cascade.Indie
{
    /// <summary>
    /// Build/stage classification, owned by the starter. Maps to concrete policy (log level …)
    /// inside <see cref="IndieBootstrapEntry" />.
    /// </summary>
    public enum BootstrapEnvironment
    {
        Dev,
        Beta,
        Gold
    }
}
