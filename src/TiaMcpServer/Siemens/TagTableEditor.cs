using Siemens.Engineering;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Changes some rows of a PLC tag table and leaves the rest as it is: all of the change, or none.</summary>
    /// <remarks>
    /// <c>CreateTag</c> never overwrites, which is right for building a table and leaves no way to fix one: a wrong
    /// address, a type, a comment, a tag no longer used. An edit names each tag it touches. Everything is checked first;
    /// each step done puts its undo on a list (<see cref="UnifiedRollback"/>), so when TIA Portal refuses a later step -
    /// an address or a type it does not take - what was done is put back: a tag added is deleted, a tag changed gets its
    /// type, address and comment back, a tag deleted is created again as it was. The tables are exported to the backup
    /// registry before anything changes, as for <c>CreateTag</c>.
    /// </remarks>
    internal sealed class TagTableEditor
    {
        private readonly PlcTagTable _table;
        private readonly TagTableEdit _edit;
        private readonly Language _language;

        /// <summary>Creates the editor of one table.</summary>
        /// <param name="table">The table.</param>
        /// <param name="edit">The change.</param>
        /// <param name="language">The project's editing language, which comments are written in.</param>
        public TagTableEditor(PlcTagTable table, TagTableEdit edit, Language language)
        {
            _table = table;
            _edit = edit;
            _language = language;
        }

        /// <summary>Checks the edit before anything changes.</summary>
        /// <exception cref="PortalException">A tag removed or set is not in the table, or a tag added takes a name in use.</exception>
        public void Check()
        {
            var missing = _edit.Touched.Where(name => _table.Tags.Find(name) == null).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Nothing was changed: '{_table.Name}' has no tag {string.Join(", ", missing.Select(name => $"'{name}'"))}");
            }

            var taken = _edit.Add.Where(row => _table.Tags.Find(row.Name) != null || _table.UserConstants.Find(row.Name) != null).Select(row => row.Name).ToList();
            if (taken.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was changed: {string.Join(", ", taken.Select(name => $"'{name}'"))} already names a tag or constant in '{_table.Name}'; change it in 'set', or remove it");
            }
        }

        /// <summary>Applies the edit; undoes what was done of it when TIA Portal refuses a step.</summary>
        /// <returns>The tags of the table afterwards, by name.</returns>
        public IReadOnlyList<string> Apply()
        {
            var done = new List<(string Name, Action Undo)>();

            try
            {
                _edit.Remove.ToList().ForEach(name => Remove(name, done));
                _edit.Set.ToList().ForEach(row => Change(row, done));
                _edit.Add.ToList().ForEach(row => Add(row, done));
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw UnifiedRollback.UndoChanges(done, failure, "changes to the table");
            }

            return _table.Tags.Select(tag => tag.Name).ToList();
        }

        private void Remove(string name, List<(string Name, Action Undo)> done)
        {
            var tag = _table.Tags.Find(name)!;
            var before = new TagTableRow(tag.Name, tag.DataTypeName, tag.LogicalAddress, Comment(tag));

            Refused(() => tag.Delete(), $"deleting '{name}'");
            done.Add(($"'{name}'", () => Create(before)));
        }

        private void Change(TagTableRow row, List<(string Name, Action Undo)> done)
        {
            var tag = _table.Tags.Find(row.Name)!;
            var before = new TagTableRow(tag.Name, tag.DataTypeName, tag.LogicalAddress, Comment(tag));

            done.Add(($"'{row.Name}'", () => Write(_table.Tags.Find(row.Name)!, before)));
            Refused(() => Write(tag, row), $"'{row.Name}' as {row.DataTypeName ?? tag.DataTypeName} at '{row.Address ?? tag.LogicalAddress}'");
        }

        private void Add(TagTableRow row, List<(string Name, Action Undo)> done)
        {
            done.Add(($"'{row.Name}'", () => _table.Tags.Find(row.Name)?.Delete()));
            Refused(() => Create(row), $"'{row.Name}' as {row.DataTypeName} at '{row.Address}'");
        }

        private void Create(TagTableRow row)
        {
            var created = _table.Tags.Create(row.Name, row.DataTypeName, row.Address);
            if (row.Comment != null)
            {
                SetComment(created, row.Comment);
            }
        }

        /// <remarks>Only what the row gives; a type and an address that change together are both written before TIA Portal checks the tag.</remarks>
        private void Write(PlcTag tag, TagTableRow row)
        {
            if (row.DataTypeName != null)
            {
                tag.DataTypeName = row.DataTypeName;
            }

            if (row.Address != null)
            {
                tag.LogicalAddress = row.Address;
            }

            if (row.Comment != null)
            {
                SetComment(tag, row.Comment);
            }
        }

        private string Comment(PlcTag tag)
        {
            return tag.Comment?.Items.Find(_language)?.Text ?? string.Empty;
        }

        private void SetComment(PlcTag tag, string comment)
        {
            var item = tag.Comment?.Items.Find(_language)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{tag.Name}' has no comment in the editing language {_language.Culture.Name}");
            item.Text = comment;
        }

        /// <remarks>TIA Portal refuses a type or an address it does not take, with a message naming neither the tag nor the value.</remarks>
        private static void Refused(Action step, string what)
        {
            try
            {
                step();
            }
            catch (Exception refused) when (!(refused is PortalException) && !UnifiedRollback.IsSessionEnded(refused))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"TIA Portal refused {what}: {refused.Message.Trim()}", null, refused);
            }
        }
    }
}
