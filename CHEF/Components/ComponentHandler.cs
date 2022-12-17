using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace CHEF.Components
{
    internal static class ComponentHandler
    {
        internal static readonly HashSet<Component> LoadedComponents = new HashSet<Component>();

        internal static async Task Init(DiscordSocketClient client)
        {
            var componentTypes = Assembly.GetExecutingAssembly().GetTypes().Where(ComponentFilter).ToList();

            foreach (var componentType in componentTypes)
            {
                try
                {
                    Logger.Log($"Enabling Component: {componentType.Name}");

                    var compInstance = (Component)Activator.CreateInstance(componentType, client);
                    if (compInstance == null) throw new ArgumentNullException(nameof(compInstance));
                    await compInstance.SetupAsync();
                    LoadedComponents.Add(compInstance);
                }
                catch (Exception e)
                {
                    Logger.Log($"Exception while trying to enable {componentType.Name} {Environment.NewLine} {e}");
                    //throw;
                }
            }

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                foreach (var component in LoadedComponents)
                {
                    component.Dispose();
                }
            };
        }

        private static bool ComponentFilter(Type type)
        {
            return type.IsSubclassOf(typeof(Component));
        }
    }
}
