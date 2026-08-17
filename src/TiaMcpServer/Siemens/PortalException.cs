using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TiaMcpServer.Siemens
{
    [Serializable]
    public class PortalException : Exception
    {
        public PortalErrorCode Code { get; }

        public IEnumerable<string>? Candidates { get; }

        public PortalException(PortalErrorCode code, string message, IEnumerable<string>? candidates = null, Exception? inner = null)
            : base(message, inner)
        {
            Code = code;
            Candidates = candidates;
        }

        /// <summary>Deserialization constructor.</summary>
        /// <param name="info">Serialized data.</param>
        /// <param name="context">Streaming context.</param>
        /// <remarks>
        /// <c>[Serializable]</c> without this is a trap rather than a feature: MSTest and the
        /// Openness callback layer both move exceptions across app domain boundaries, and without
        /// it the real exception is replaced by a <c>SerializationException</c> complaining that
        /// no constructor was found. That is how the message naming an unanswered download prompt
        /// went missing — the failure reported the plumbing instead of the problem.
        /// </remarks>
        protected PortalException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Code = (PortalErrorCode)info.GetInt32(nameof(Code));
        }

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (info == null)
            {
                return;
            }

            info.AddValue(nameof(Code), (int)Code);
        }
    }
}

