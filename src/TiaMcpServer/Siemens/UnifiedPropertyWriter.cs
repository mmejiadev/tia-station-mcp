using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Sets a WinCC Unified property by its path, <c>BackColor</c> or <c>Font.Size</c>.</summary>
    /// <remarks>
    /// Every property is a plain CLR property of the Openness type, so a path is resolved by reflection,
    /// and it can be resolved against the type before anything exists: <see cref="Check"/> runs over the
    /// whole description before the first item is created, so a typo refuses the call instead of leaving
    /// half a screen. A part (<c>Font</c>, <c>Padding</c>) is read-only and is walked into; a text is a
    /// <see cref="MultilingualText"/> and is set in the project's editing language.
    /// </remarks>
    internal sealed class UnifiedPropertyWriter
    {
        private const int MaxNamesListed = 60;

        private readonly Language _language;

        /// <summary>Creates a writer.</summary>
        /// <param name="language">The project's editing language, which texts are written in.</param>
        public UnifiedPropertyWriter(Language language)
        {
            _language = language;
        }

        /// <summary>Refuses a setting the type cannot take, before anything is changed.</summary>
        /// <param name="ownerType">The Openness type the setting applies to.</param>
        /// <param name="setting">The setting.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what is wrong.</exception>
        public static void Check(Type ownerType, UnifiedPropertySetting setting)
        {
            var property = ResolveOnType(ownerType, setting.Segments, setting.Path);

            if (property.PropertyType == typeof(MultilingualText))
            {
                RequireText(setting);
                return;
            }

            RequireWritable(property, setting.Path);
            UnifiedPropertyConverter.Convert(property.PropertyType, setting.Value, setting.Path);
        }

        /// <summary>Sets a property on an item or a screen.</summary>
        /// <param name="owner">The item or the screen.</param>
        /// <param name="setting">The setting, already passed through <see cref="Check"/>.</param>
        public void Apply(object owner, UnifiedPropertySetting setting)
        {
            var target = ResolveOwner(owner, setting.Segments);
            var property = Find(target.GetType(), setting.Segments[setting.Segments.Count - 1]);

            if (property.PropertyType == typeof(MultilingualText))
            {
                SetText((MultilingualText)Read(property, target), RequireText(setting), setting.Path);
                return;
            }

            var value = UnifiedPropertyConverter.Convert(property.PropertyType, setting.Value, setting.Path);

            Invoke(() => property.SetValue(target, value));
        }

        /// <summary>The object that holds the last segment of a path: the item itself, or one of its parts.</summary>
        /// <param name="owner">The item or the screen.</param>
        /// <param name="segments">The path's segments.</param>
        /// <returns>The object the last segment is a property of.</returns>
        public static object ResolveOwner(object owner, IReadOnlyList<string> segments)
        {
            var current = owner;

            foreach (var segment in segments.Take(segments.Count - 1))
            {
                current = Read(Find(current.GetType(), segment), current);
            }

            return current;
        }

        /// <summary>Finds the property a path names, on a type.</summary>
        /// <param name="type">The Openness type.</param>
        /// <param name="segments">The path's segments.</param>
        /// <param name="path">The path, for the refusal.</param>
        /// <returns>The property.</returns>
        public static PropertyInfo ResolveOnType(Type type, IReadOnlyList<string> segments, string path)
        {
            var current = type;

            foreach (var segment in segments.Take(segments.Count - 1))
            {
                current = Find(current, segment).PropertyType;
            }

            return Find(current, segments[segments.Count - 1], path);
        }

        private static PropertyInfo Find(Type type, string name, string? path = null)
        {
            return type.GetProperties().FirstOrDefault(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"{type.Name} has no property '{name}'{(path == null ? string.Empty : $" (in '{path}')")}. "
                    + $"It has: {string.Join(", ", type.GetProperties().Select(property => property.Name).OrderBy(n => n, StringComparer.Ordinal).Take(MaxNamesListed))}");
        }

        /// <summary>Why a value property cannot be set, or null when it can.</summary>
        /// <param name="property">The property, as reflected on the item's or part's own type.</param>
        /// <returns>The reason, or null.</returns>
        public static string? SetterRefusal(PropertyInfo property)
        {
            if (!property.CanWrite)
            {
                return "read-only: TIA Portal sets it";
            }

            return UnifiedRefusedSetters.ReasonFor(property.ReflectedType?.Name ?? string.Empty, property.Name);
        }

        private static void RequireWritable(PropertyInfo property, string path)
        {
            if (!property.CanWrite && !UnifiedPropertyConverter.Supports(property.PropertyType))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{path}' is a group of properties, not a value: set one of its own, e.g. '{path}.{FirstSettable(property.PropertyType)}'");
            }

            if (SetterRefusal(property) is string refusal)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' cannot be set: {refusal}");
            }
        }

        private static string FirstSettable(Type type)
        {
            return type.GetProperties().FirstOrDefault(property => property.CanWrite && UnifiedPropertyConverter.Supports(property.PropertyType))?.Name ?? "…";
        }

        private static string RequireText(UnifiedPropertySetting setting)
        {
            return setting.Value.ValueKind == JsonValueKind.String
                ? setting.Value.GetString()!
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{setting.Path}' is a text and takes a string; got {setting.Value.GetRawText()}");
        }

        private void SetText(MultilingualText text, string value, string path)
        {
            var item = text.Items.Find(_language)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{path}' has no entry for the project's editing language {_language.Culture.Name}");

            item.Text = UnifiedRichText.Format(value);
        }

        private static object Read(PropertyInfo property, object owner)
        {
            object? value = null;
            Invoke(() => value = property.GetValue(owner));

            return value ?? throw new PortalException(PortalErrorCode.InvalidState, $"{owner.GetType().Name}.{property.Name} is empty");
        }

        /// <summary>Lets the exception Openness threw through, not the reflection wrapper around it.</summary>
        /// <remarks>
        /// A <see cref="TargetInvocationException"/> would hide a <c>NonRecoverableException</c> from the
        /// check that forgets an ended session, and would report every refusal as "an exception was thrown
        /// by the target of an invocation".
        /// </remarks>
        private static void Invoke(Action call)
        {
            try
            {
                call();
            }
            catch (TargetInvocationException wrapper) when (wrapper.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(wrapper.InnerException).Throw();
            }
        }
    }
}
