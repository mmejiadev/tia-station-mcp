using System;
using System.Collections.Generic;
using System.Text;

namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// Joins arguments into a Windows command line that survives being parsed back apart.
    /// </summary>
    /// <remarks>
    /// .NET Framework 4.8 has no <c>ProcessStartInfo.ArgumentList</c> — it arrived in .NET Core —
    /// so a command line has to be built as one string, and building one by concatenating with
    /// spaces is how an argument containing a space silently becomes two.
    ///
    /// The rules implemented here are the ones <c>CommandLineToArgvW</c> applies in reverse, and
    /// they are not obvious: a backslash is literal except immediately before a quote, where it
    /// doubles. A block path such as <c>Program blocks\FB_Station</c> and a value summary
    /// containing a quote both go through here, which is why it is a class with its own tests
    /// rather than three lines inside the caller.
    /// </remarks>
    public static class CommandLine
    {
        /// <summary>Joins arguments into a single command line.</summary>
        /// <param name="arguments">The arguments, in order.</param>
        /// <returns>A command line a Windows process will parse back into exactly these arguments.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
        public static string Join(IEnumerable<string> arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            var line = new StringBuilder();

            foreach (var argument in arguments)
            {
                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                Append(line, argument ?? string.Empty);
            }

            return line.ToString();
        }

        /// <summary>Appends one argument, quoted only when it has to be.</summary>
        /// <param name="line">The line being built.</param>
        /// <param name="argument">The argument to append.</param>
        /// <remarks>
        /// An empty argument is quoted rather than omitted. Dropping it would shift every argument
        /// after it by one position, which is the kind of failure that shows up as a lookup asking
        /// the wrong question rather than as an error.
        /// </remarks>
        private static void Append(StringBuilder line, string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                line.Append(argument);

                return;
            }

            line.Append('"');

            for (var index = 0; index < argument.Length; index++)
            {
                var backslashes = CountBackslashesAt(argument, index);

                index += backslashes;

                if (index == argument.Length)
                {
                    // Trailing backslashes sit immediately before the closing quote, so they double.
                    line.Append('\\', backslashes * 2);

                    break;
                }

                line.Append('\\', argument[index] == '"' ? (backslashes * 2) + 1 : backslashes);
                line.Append(argument[index]);
            }

            line.Append('"');
        }

        private static int CountBackslashesAt(string argument, int index)
        {
            var start = index;

            while (index < argument.Length && argument[index] == '\\')
            {
                index++;
            }

            return index - start;
        }
    }
}
