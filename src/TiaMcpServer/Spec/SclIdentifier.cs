using System;
using System.Linq;

namespace TiaMcpServer.Spec
{
    /// <summary>
    /// Checks that a name from a specification can be a name in SCL.
    /// </summary>
    /// <remarks>
    /// A station called "Drill 1" or "Estacion-2" generates code that does not compile, and the
    /// compiler then reports a syntax error in a block nobody typed. Refusing the name here says what
    /// is actually wrong.
    ///
    /// Letters rather than ASCII letters, so "Estacion" with an accent is accepted: TIA Portal allows
    /// it and rejecting it would be this file inventing a rule the platform does not have.
    /// </remarks>
    internal static class SclIdentifier
    {
        /// <summary>Throws unless the name can be used as an SCL identifier.</summary>
        /// <param name="name">The name to check.</param>
        /// <param name="what">What it names, for the message: "station", "cell".</param>
        /// <param name="parameterName">The parameter to blame.</param>
        /// <exception cref="ArgumentException">The name cannot be an SCL identifier.</exception>
        internal static void Require(string name, string what, string parameterName)
        {
            if (char.IsDigit(name[0]))
            {
                throw new ArgumentException(
                    $"A {what} name cannot start with a digit: '{name}'", parameterName);
            }

            var offending = name.FirstOrDefault(character => !IsAllowed(character));

            if (offending != default)
            {
                throw new ArgumentException(
                    $"A {what} name may only contain letters, digits and underscores, so it can be used "
                    + $"as an SCL identifier. '{name}' contains '{offending}'.",
                    parameterName);
            }
        }

        private static bool IsAllowed(char character)
        {
            return char.IsLetterOrDigit(character) || character == '_';
        }
    }
}
