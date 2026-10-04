using Microsoft.Extensions.Logging;
using System;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Its own file because <c>PortalProject.cs</c> is already past the size this repository allows.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Describes the open project: where it is, who made it and who last changed it.</summary>
        /// <returns>The summary, or null when no project is open.</returns>
        /// <exception cref="PortalException">TIA Portal could not be asked.</exception>
        /// <remarks>
        /// Read from <c>ProjectBase</c>, which a local project and a multiuser one both are, through
        /// its typed properties rather than its attribute bag, so that a renamed attribute is a
        /// build error instead of an empty field.
        /// </remarks>
        public ProjectSummary? GetProjectSummary()
        {
            try
            {
                if (_project == null)
                {
                    return null;
                }

                return new ProjectSummary(
                    _project.Path.FullName,
                    _project.Name,
                    _project.Author,
                    new DateTimeOffset(_project.CreationTime),
                    new DateTimeOffset(_project.LastModified),
                    _project.LastModifiedBy);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the project failed: {ex.Message}", null, ex);

                _logger?.LogError(pex, "GetProjectSummary failed");
                throw pex;
            }
        }
    }
}
