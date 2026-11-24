using Siemens.Engineering.Library.Types;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Moving a program's instance of a block or PLC data type type to another version: it is deleted and created again from
    /// that version, in its group, as <c>UpdateProject</c> was measured to remove instances instead (see PortalLibraryInstances).
    /// The program is compiled before and after, and when it has more errors after, every instance goes back.
    /// </remarks>
    public partial class Portal
    {
        private int MoveCheckingCompile(string softwarePath, (LibraryType Type, Version Number) target, IReadOnlyList<PlcInstance> moving, int errorsBefore)
        {
            var moved = new List<MovedInstance>();

            try
            {
                foreach (var instance in moving)
                {
                    moved.Add(MoveInstance(instance, (FindVersion(target.Type, target.Number), FindVersion(target.Type, instance.Shown))));
                }
            }
            catch (Exception failure) when (IsMoveFailure(failure))
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"Moving the instances of '{target.Type.Name}' failed: {failure.Message.Trim()} Of those moved before it, {PutInstancesBack(target.Type, moved)}", null, failure);
            }

            var errorsAfter = CompileOrPutBack(softwarePath, target.Type, moved);
            if (errorsAfter <= errorsBefore)
            {
                return errorsAfter;
            }

            var outcome = PutInstancesBack(target.Type, moved);
            CompileSoftware(softwarePath);

            throw new PortalException(PortalErrorCode.InvalidState, $"With {LibraryVersionNumber.Format(target.Number)} '{softwarePath}' compiles with {errorsAfter} error(s), against {errorsBefore} before, so what uses '{target.Type.Name}' does not fit it; of the instances moved, {outcome}");
        }

        /// <remarks>The instances are moved by now, so a compile that fails puts them back before the failure goes on.</remarks>
        private int CompileOrPutBack(string softwarePath, LibraryType type, IReadOnlyList<MovedInstance> moved)
        {
            try
            {
                return CompileSoftware(softwarePath).ErrorCount;
            }
            catch (PortalException failure) when (!IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"Compiling '{softwarePath}' after moving the instances of '{type.Name}' failed: {failure.Message.Trim()} Of the instances moved, {PutInstancesBack(type, moved)}", null, failure);
            }
        }

        /// <remarks>
        /// A refusal of this server's own, as well as one of TIA Portal's: either way what was moved before it goes back. Not an
        /// ended session, which goes on up so the project is forgotten.
        /// </remarks>
        private static bool IsMoveFailure(Exception failure)
        {
            return failure is PortalException || IsUndoableFailure(failure);
        }

        /// <remarks>Deleted before it is created again: a program keeps one block or type per name, and creating first would rename the new one.</remarks>
        private static MovedInstance MoveInstance(PlcInstance instance, (LibraryTypeVersion Target, LibraryTypeVersion Previous) versions)
        {
            switch (instance.Info.LibraryTypeInstance)
            {
                case PlcBlock block:
                    var blocks = (PlcBlockGroup)block.Parent;
                    block.Delete();
                    Func<LibraryTypeVersion, Action> createBlock = version => blocks.Blocks.CreateFrom((CodeBlockLibraryTypeVersion)version).Delete;
                    return new MovedInstance(instance.Shown, createBlock, CreateOrRestore(createBlock, versions));
                case PlcType type:
                    var types = (PlcTypeGroup)type.Parent;
                    type.Delete();
                    Func<LibraryTypeVersion, Action> createType = version => types.Types.CreateFrom((PlcTypeLibraryTypeVersion)version).Delete;
                    return new MovedInstance(instance.Shown, createType, CreateOrRestore(createType, versions));
                default:
                    throw new PortalException(PortalErrorCode.InvalidState, $"An instance of kind {instance.Info.LibraryTypeInstance?.GetType().Name ?? "unknown"} is not one this tool moves");
            }
        }

        /// <remarks>The instance is deleted by now; when the new version will not take its place, the one it showed does, before the failure goes on.</remarks>
        private static Action CreateOrRestore(Func<LibraryTypeVersion, Action> create, (LibraryTypeVersion Target, LibraryTypeVersion Previous) versions)
        {
            try
            {
                return create(versions.Target);
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"An instance was deleted and TIA Portal did not create it again from the new version ({failure.Message.Trim()}); {RestorePrevious(create, versions.Previous)}.", null, failure);
            }
        }

        /// <returns>What became of the deleted instance, for the failure to say; a failure here must not replace the one being reported.</returns>
        private static string RestorePrevious(Func<LibraryTypeVersion, Action> create, LibraryTypeVersion previous)
        {
            try
            {
                create(previous);
                return "it was created again from the version it showed";
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                return $"creating it again from the version it showed failed too ({failure.Message.Trim()}), so it is missing from the program: the program before is in the backup registry";
            }
        }

        /// <returns>What became of the instances, for the refusal to say; a failure here must not replace the failure being reported.</returns>
        private static string PutInstancesBack(LibraryType type, IReadOnlyList<MovedInstance> moved)
        {
            try
            {
                foreach (var instance in moved.Reverse())
                {
                    instance.DeleteCurrent();
                    instance.CreateFrom(FindVersion(type, instance.From));
                }

                return "every one is back on the version it showed";
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                return $"putting them back failed too ({failure.Message.Trim()}): the program before is in the backup registry";
            }
        }

        private static JsonObject DescribeMoved(string typeName, Version number, IReadOnlyList<(string Name, Version From)> moving, (int Before, int After) errors)
        {
            return new JsonObject
            {
                ["type"] = typeName,
                ["version"] = LibraryVersionNumber.Format(number),
                ["moved"] = new JsonArray(moving.Select(instance => (JsonNode)new JsonObject
                {
                    ["name"] = instance.Name,
                    ["from"] = LibraryVersionNumber.Format(instance.From)
                }).ToArray()),
                ["compile"] = new JsonObject { ["errorsBefore"] = errors.Before, ["errorsAfter"] = errors.After }
            };
        }

        /// <remarks>Read before anything moves: a deleted instance has no name to read.</remarks>
        private static List<(string Name, Version From)> NameInstances(IEnumerable<PlcInstance> instances)
        {
            return instances.Select(instance => (instance.Info.LibraryTypeInstance switch
            {
                PlcBlock block => block.Name,
                PlcType type => type.Name,
                _ => instance.Info.LibraryTypeInstance?.GetType().Name ?? "unknown"
            }, instance.Shown)).ToList();
        }

        /// <summary>An instance created again from another version, and how to delete it and create it from yet another.</summary>
        private sealed class MovedInstance
        {
            public MovedInstance(Version from, Func<LibraryTypeVersion, Action> createFrom, Action deleteCurrent)
            {
                From = from;
                CreateFrom = createFrom;
                DeleteCurrent = deleteCurrent;
            }

            public Version From { get; }

            /// <summary>Creates the instance from a version, in its group, and gives back what deletes that one.</summary>
            public Func<LibraryTypeVersion, Action> CreateFrom { get; }

            public Action DeleteCurrent { get; }
        }
    }
}
