using System;
using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Server;

namespace TiaMcpServer.OpcUa.Test.TestServer
{
    /// <summary>
    /// The address space of the test server: one folder shaped like a Siemens data block.
    /// </summary>
    /// <remarks>
    /// The identifiers carry quotes, <c>"DB_Cell"."Running"</c>, because that is how an S7-1500
    /// names a data block member, and the quotes are the part most likely to be lost between a
    /// browse and a read.
    /// </remarks>
    // CA2000 is off for this class alone. Every node state created here is handed to the node
    // manager through AddPredefinedNode or AddChild, which owns it from then on and releases it when
    // the server stops; disposing it here would pull a node out from under a running server.
#pragma warning disable CA2000
    internal sealed class CellNodeManager : CustomNodeManager2
    {
        internal const string NamespaceUri = "urn:tia-station-mcp:test-cell";

        internal const string FolderId = "\"DB_Cell\"";
        internal const string RunningId = "\"DB_Cell\".\"Running\"";
        internal const string SpeedId = "\"DB_Cell\".\"Speed\"";
        internal const string PieceCountId = "\"DB_Cell\".\"PieceCount\"";
        internal const string StationStepsId = "\"DB_Cell\".\"StationSteps\"";

        internal CellNodeManager(IServerInternal server, ApplicationConfiguration configuration)
            : base(server, configuration, NamespaceUri)
        {
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                var folder = CreateFolder();
                AddExternalReference(ObjectIds.ObjectsFolder, ReferenceTypeIds.Organizes, false, folder.NodeId, externalReferences);

                AddVariable(folder, RunningId, "Running", DataTypeIds.Boolean, ValueRanks.Scalar, true);
                AddVariable(folder, SpeedId, "Speed", DataTypeIds.Double, ValueRanks.Scalar, 0.5);
                AddVariable(folder, PieceCountId, "PieceCount", DataTypeIds.Int16, ValueRanks.Scalar, (short)42);
                AddVariable(folder, StationStepsId, "StationSteps", DataTypeIds.Int16, ValueRanks.OneDimension, new short[] { 1, 2, 3, 4 });

                AddPredefinedNode(SystemContext, folder);
            }
        }

        private FolderState CreateFolder()
        {
            var folder = new FolderState(null)
            {
                SymbolicName = "DB_Cell",
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = ObjectTypeIds.FolderType,
                NodeId = new NodeId(FolderId, NamespaceIndex),
                BrowseName = new QualifiedName("DB_Cell", NamespaceIndex),
                DisplayName = new LocalizedText("DB_Cell"),
                EventNotifier = EventNotifiers.None
            };

            folder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);

            return folder;
        }

        private void AddVariable(FolderState folder, string identifier, string name, NodeId dataType, int valueRank, object value)
        {
            var variable = new BaseDataVariableState(folder)
            {
                SymbolicName = name,
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId(identifier, NamespaceIndex),
                BrowseName = new QualifiedName(name, NamespaceIndex),
                DisplayName = new LocalizedText(name),
                DataType = dataType,
                ValueRank = valueRank,
                AccessLevel = AccessLevels.CurrentRead,
                UserAccessLevel = AccessLevels.CurrentRead,
                Value = value,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };

            folder.AddChild(variable);
        }
    }
#pragma warning restore CA2000
}
