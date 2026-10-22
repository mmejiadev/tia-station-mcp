using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Shapes;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Draws one WinCC Unified screen item: creates it, sets its properties, its points and its tag links.</summary>
    /// <remarks>
    /// Openness creates an item with a generic method, <c>ScreenItems.Create&lt;T&gt;(name)</c>, and the
    /// type comes from the description, so the method is closed over it by reflection. That is what lets
    /// one writer draw every type in <see cref="UnifiedItemTypes"/> rather than one method per type.
    /// </remarks>
    internal sealed class UnifiedScreenItemWriter
    {
        private const string PointsProperty = "Points";
        private const string DynamizationsProperty = "Dynamizations";

        private static readonly MethodInfo CreateItem = typeof(HmiScreenItemBaseComposition).GetMethods()
            .Single(method => method.Name == "Create" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1);

        private static readonly MethodInfo CreateDynamization = typeof(DynamizationBaseComposition).GetMethods()
            .Single(method => method.Name == "Create" && method.IsGenericMethodDefinition);

        private readonly UnifiedPropertyWriter _properties;

        /// <summary>Creates an item writer.</summary>
        /// <param name="properties">Sets the properties.</param>
        public UnifiedScreenItemWriter(UnifiedPropertyWriter properties)
        {
            _properties = properties;
        }

        /// <summary>Refuses an item this Openness cannot draw as described, before anything is changed.</summary>
        /// <param name="item">The item.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what is wrong.</exception>
        public static void Check(UnifiedItemDescription item)
        {
            var type = ResolveType(item.TypeName);

            foreach (var setting in item.Properties)
            {
                UnifiedPropertyWriter.Check(type, setting);
            }

            foreach (var path in item.Tags.Keys)
            {
                RequireLinkable(type, path);
            }
        }

        /// <remarks>
        /// A link is made on the object that holds the property, through its <c>Dynamizations</c>; a part
        /// without them (a font) is refused here, before the screen's items are deleted, not while drawing.
        /// </remarks>
        private static void RequireLinkable(Type type, string path)
        {
            var property = UnifiedPropertyWriter.ResolveOnType(type, path.Split('.'), path);
            var owner = property.ReflectedType!;

            if (owner.GetProperty(DynamizationsProperty) == null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{path}' cannot be linked to a tag: {owner.Name} takes no links; link a property of the item itself");
            }
        }

        /// <summary>Draws an item on a screen.</summary>
        /// <param name="screen">The screen.</param>
        /// <param name="item">The item, already passed through <see cref="Check"/>.</param>
        public void Draw(HmiScreen screen, UnifiedItemDescription item)
        {
            var created = Invoke(CreateItem.MakeGenericMethod(ResolveType(item.TypeName)), screen.ScreenItems, item.Name);

            foreach (var setting in item.Properties)
            {
                _properties.Apply(created, setting);
            }

            if (item.Points.Count > 0)
            {
                ReplacePoints(created, item);
            }

            foreach (var link in item.Tags)
            {
                BindTag(created, link.Key, link.Value);
            }
        }

        /// <summary>The Openness type of an item.</summary>
        /// <param name="fullName">The full type name, from <see cref="UnifiedItemTypes"/>.</param>
        /// <returns>The type.</returns>
        public static Type ResolveType(string fullName)
        {
            return typeof(HmiScreenItemBase).Assembly.GetType(fullName, throwOnError: false)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"This TIA Portal's Openness has no type '{fullName}'");
        }

        /// <remarks>
        /// A new polyline comes with two points of its own; the description's are the only ones kept. The new
        /// points go in before the old ones come out, because TIA Portal refuses to delete a point that would
        /// leave fewer than two (measured 2026-10-21), and the description always has at least two.
        /// </remarks>
        private static void ReplacePoints(object created, UnifiedItemDescription item)
        {
            var points = (HmiPointComposition)created.GetType().GetProperty(PointsProperty)!.GetValue(created);
            var previous = points.ToList();

            foreach (var point in item.Points)
            {
                points.Create(point.X, point.Y);
            }

            foreach (var existing in previous)
            {
                existing.Delete();
            }
        }

        private static void BindTag(object created, string path, string tag)
        {
            var segments = path.Split('.');
            var owner = UnifiedPropertyWriter.ResolveOwner(created, segments);
            var dynamizations = owner.GetType().GetProperty(DynamizationsProperty)?.GetValue(owner) as DynamizationBaseComposition
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' cannot be linked to a tag");

            var dynamization = (TagDynamization)Invoke(CreateDynamization.MakeGenericMethod(typeof(TagDynamization)), dynamizations, segments.Last());

            dynamization.Tag = tag;
        }

        private static object Invoke(MethodInfo method, object target, string argument)
        {
            try
            {
                return method.Invoke(target, new object[] { argument });
            }
            catch (TargetInvocationException wrapper) when (wrapper.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(wrapper.InnerException).Throw();
                throw;
            }
        }
    }
}
