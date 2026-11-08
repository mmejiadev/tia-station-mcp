using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Secrets
{
    /// <summary>The command a person runs in their own terminal to manage secrets: <c>TiaMcpServer secret set NAME</c>.</summary>
    /// <remarks>
    /// The value never reaches a model, which is why no MCP tool takes one: what goes through a tool stays in the conversation.
    /// It is typed here, hidden, and never read from the command line, which would leave it in the shell's history.
    /// </remarks>
    public sealed class SecretCommand
    {
        /// <summary>The first argument that selects this command.</summary>
        public const string Verb = "secret";

        private const string ReplaceFlag = "--replace";

        private const string Usage = "Usage: TiaMcpServer secret set NAME [--replace] | list | delete NAME. The value is asked for, hidden; never give it on the command line, where it would stay in the shell's history.";

        private readonly ISecretStore _store;

        private readonly ISecretConsole _console;

        /// <summary>Creates the command.</summary>
        /// <param name="store">Where secrets are kept.</param>
        /// <param name="console">The terminal.</param>
        public SecretCommand(ISecretStore store, ISecretConsole console)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        /// <summary>Whether these arguments ask for this command.</summary>
        /// <param name="args">The command line.</param>
        /// <returns>True when they do.</returns>
        public static bool IsFor(IReadOnlyList<string> args)
        {
            return args != null && args.Count > 0 && string.Equals(args[0], Verb, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Runs the command.</summary>
        /// <param name="args">The command line, with <see cref="Verb"/> first.</param>
        /// <returns>The process exit code.</returns>
        public int Run(IReadOnlyList<string> args)
        {
            try
            {
                return Dispatch(args ?? Array.Empty<string>());
            }
            catch (Exception failure) when (failure is PortalException || failure is IOException || failure is UnauthorizedAccessException || failure is CryptographicException)
            {
                _console.WriteLine(failure.Message);

                return 1;
            }
        }

        private int Dispatch(IReadOnlyList<string> args)
        {
            var action = args.Count > 1 ? args[1].ToUpperInvariant() : string.Empty;

            switch (action)
            {
                case "SET" when args.Count == 3 || (args.Count == 4 && args[3] == ReplaceFlag):
                    return Set(args[2], args.Count == 4);
                case "LIST" when args.Count == 2:
                    return List();
                case "DELETE" when args.Count == 3:
                    return Delete(args[2]);
                default:
                    // Nothing of what was typed is repeated: it may be the value.
                    _console.WriteLine(Usage);
                    return 1;
            }
        }

        private int Set(string name, bool replace)
        {
            if (!_console.IsInteractive)
            {
                _console.WriteLine("A secret is typed by a person: run this in your own terminal, with nothing piped into it.");
                return 1;
            }

            _store.CheckCanStore(name, replace);

            using var value = _console.ReadHidden("Value: ");
            using var again = _console.ReadHidden("Value again: ");

            if (!SecretBytes.AreEqual(value, again))
            {
                _console.WriteLine("The two values differ; nothing was stored.");
                return 1;
            }

            _store.Store(name, value, replace);
            _console.WriteLine("Secret stored, encrypted for this Windows account.");

            return 0;
        }

        private int List()
        {
            foreach (var name in _store.Names())
            {
                _console.WriteLine(name);
            }

            return 0;
        }

        private int Delete(string name)
        {
            _store.Delete(name);
            _console.WriteLine("Secret deleted.");

            return 0;
        }
    }
}
