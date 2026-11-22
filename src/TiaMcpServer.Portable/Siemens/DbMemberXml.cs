using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Applies a <see cref="DbMemberEdit"/> to the SimaticML export of a global data block.</summary>
    /// <remarks>
    /// Measured 2026-11-22 on V21: a global DB exported, one member changed in the XML and imported again (the import replaces
    /// the block of the name) keeps its number and every start value the XML holds. So an edit is a change to the XML and
    /// nothing else: what is not named stays as exported. Only the members of the <c>Static</c> section's own level; a
    /// struct's members, and the per-element start values of an array, are left to TIA Portal.
    /// </remarks>
    public static class DbMemberXml
    {
        private const string StaticSection = "Static";

        private static readonly XNamespace Interface = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

        /// <summary>Applies an edit to the export of a data block.</summary>
        /// <param name="blockXml">The block as TIA Portal exported it.</param>
        /// <param name="edit">The members to remove, change and add.</param>
        /// <returns>The block's XML, to import in place of the one exported.</returns>
        /// <exception cref="PortalException">A member to remove or change is missing, one to add is there, or a member is one this edit does not change.</exception>
        public static string Apply(string blockXml, DbMemberEdit edit)
        {
            _ = edit ?? throw new ArgumentNullException(nameof(edit));
            var document = Load(blockXml);
            var section = FindStaticSection(document);

            foreach (var name in edit.Remove)
            {
                Find(section, name).Remove();
            }

            foreach (var row in edit.Set)
            {
                Change(Find(section, row.Name), row);
            }

            foreach (var row in edit.Add)
            {
                Append(section, row);
            }

            return Declaration + Environment.NewLine + document.Root;
        }

        private static string Declaration => new XDeclaration("1.0", "utf-8", null).ToString();

        private static XDocument Load(string blockXml)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

            try
            {
                using var reader = XmlReader.Create(new StringReader(blockXml ?? string.Empty), settings);

                return XDocument.Load(reader);
            }
            catch (XmlException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The exported block is not valid XML: {exception.Message}", null, exception);
            }
        }

        private static XElement FindStaticSection(XDocument document)
        {
            return document.Descendants(Interface + "Section").FirstOrDefault(section => (string?)section.Attribute("Name") == StaticSection)
                ?? throw new PortalException(PortalErrorCode.InvalidState, "The block has no 'Static' section: it is not a global data block");
        }

        /// <remarks>Case aside, as TIA Portal compares member names; the refusal lists the members there are.</remarks>
        private static XElement Find(XElement section, string name)
        {
            return section.Elements(Interface + "Member").FirstOrDefault(member => string.Equals((string?)member.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The block has no member '{name}'. Members: {string.Join(", ", section.Elements(Interface + "Member").Select(member => (string?)member.Attribute("Name")))}");
        }

        private static void Change(XElement member, DbMemberRow row)
        {
            RequireOwnLevel(member, row.Name);

            if (row.DataType != null)
            {
                member.SetAttributeValue("Datatype", row.DataType);
                member.Elements(Interface + "Subelement").Remove();

                if (row.StartValue == null)
                {
                    member.Elements(Interface + "StartValue").Remove();
                }
            }

            if (row.StartValue != null)
            {
                SetStartValue(member, row);
            }
        }

        /// <remarks>A struct holds members of its own, an array start values per element: neither is one value to set.</remarks>
        private static void RequireOwnLevel(XElement member, string name)
        {
            if (member.Elements(Interface + "Member").Any())
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' is a struct with members of its own, which this edit does not change; change them in TIA Portal");
            }
        }

        private static void SetStartValue(XElement member, DbMemberRow row)
        {
            if (member.Elements(Interface + "Subelement").Any())
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{row.Name}' has start values per element, which this edit does not change; change them in TIA Portal");
            }

            var start = member.Element(Interface + "StartValue");
            if (start != null)
            {
                start.Value = row.StartValue!;
                return;
            }

            var created = new XElement(Interface + "StartValue", row.StartValue);
            var comment = member.Element(Interface + "Comment");
            if (comment != null)
            {
                comment.AddAfterSelf(created);
                return;
            }

            member.AddFirst(created);
        }

        private static void Append(XElement section, DbMemberRow row)
        {
            if (section.Elements(Interface + "Member").Any(member => string.Equals((string?)member.Attribute("Name"), row.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The block has a member '{row.Name}' already; 'set' changes it");
            }

            var added = new XElement(Interface + "Member", new XAttribute("Name", row.Name), new XAttribute("Datatype", row.DataType!));
            if (row.StartValue != null)
            {
                added.Add(new XElement(Interface + "StartValue", row.StartValue));
            }

            section.Add(added);
        }
    }
}
