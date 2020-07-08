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
                    await compInstance.SetupAsync();
                }
                catch (Exception e)
                {
                    Logger.Log($"Exception while trying to enable {componentType.Name} {Environment.NewLine} {e}");
                    //throw;
                }
            }
        }

        private static bool ComponentFilter(Type type)
        {
            return type.IsSubclassOf(typeof(Component));
        }
    }
}
