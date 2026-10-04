namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Which TIA Portal project the server is working on, as the guard sees it.
    /// </summary>
    /// <remarks>
    /// An interface here because the governance layer must not reference Openness: the server
    /// implements it over the open project, and the tests over a fixed path. The guard asks it when a
    /// change is proposed, and records the answer in the plan and on every line of the trail that
    /// plan writes; and asks again when a plan is confirmed, refusing it when the project open then
    /// is not the one it was proposed in.
    /// </remarks>
    public interface IProjectContext
    {
        /// <summary>The open project's file path, or empty when no project is open.</summary>
        string CurrentProjectPath { get; }
    }
}
