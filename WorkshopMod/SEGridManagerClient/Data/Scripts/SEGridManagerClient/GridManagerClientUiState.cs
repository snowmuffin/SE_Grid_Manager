using System;
using System.Collections.Generic;

namespace SEGridManagerClient
{
    /// <summary>
    /// Shared state between mission/chat UI and Rich HUD terminal so both paths see the same lists and targets.
    /// </summary>
    internal static class GridManagerClientUiState
    {
        public static readonly List<GridManagerParse.GridRow> LastGrids = new List<GridManagerParse.GridRow>();
        public static long PendingGetBlocksForGridId;
        public static string PendingGetBlocksGridName = string.Empty;
        public static long CurrentDetailGridId;
        public static string CurrentDetailGridName = string.Empty;
        public static string LastBlocksMainOwner = string.Empty;
        public static readonly Dictionary<string, int> LastBlockCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>When Rich HUD is active: "grids" or "blocks" to open that terminal page after StateChanged. Consumed by GridManagerRichHudSession.</summary>
        public static string PendingTerminalPage = string.Empty;

        /// <summary>Raised on the game thread after shared fields are updated from a server reply.</summary>
        public static event Action StateChanged;

        public static void SetLastGridsFromWorkList(List<GridManagerParse.GridRow> work)
        {
            LastGrids.Clear();
            for (int i = 0; i < work.Count; i++)
            {
                LastGrids.Add(work[i]);
            }
        }

        public static void SetLastBlocks(string mainOwner, Dictionary<string, int> blocks)
        {
            LastBlocksMainOwner = mainOwner ?? string.Empty;
            LastBlockCounts.Clear();
            if (blocks == null)
            {
                return;
            }

            foreach (var kv in blocks)
            {
                if (!string.IsNullOrEmpty(kv.Key))
                {
                    LastBlockCounts[kv.Key] = kv.Value;
                }
            }
        }

        public static void RaiseStateChanged()
        {
            var h = StateChanged;
            if (h != null)
            {
                h();
            }
        }
    }
}
