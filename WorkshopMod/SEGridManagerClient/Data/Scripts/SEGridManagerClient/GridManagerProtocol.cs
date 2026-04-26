// Protocol constants — must stay in sync with TorchPlugin/Plugin.cs on the server.
namespace SEGridManagerClient
{
    internal static class GridManagerProtocol
    {
        public const ushort MsgGetBlocks = 42424;
        public const ushort MsgGetGrids = 42425;
        public const ushort MsgBlockDelete = 42426;
    }
}
