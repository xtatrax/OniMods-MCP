using System.Text;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static void AppendCellInfrastructureDetailHint(StringBuilder sb, int cell)
        {
            bool hasLine = HasLayer(cell, PowerLayers)
                || HasLayer(cell, LiquidLayers)
                || HasLayer(cell, GasLayers)
                || HasLayer(cell, LogicLayers)
                || HasLayer(cell, ConveyorLayers);

            string bridge = BridgeText(cell, default(HashedString));
            if (!hasLine && string.IsNullOrEmpty(bridge))
                return;

            sb.AppendLine("- 読み方: glyph=このセルの上下左右の接続, dirs=接続済みの方向, open=隣接セルに経路があるが未接続の方向, to=接続済み隣接セルの座標");
            sb.AppendLine("- ポート: `⊗`=入力/電力消費/入口, `⊙`=出力/発電/出口, `⌒`=ブリッジや交差設備の基点。ポート座標は Endpoint 行を正とする");
            sb.AppendLine("- ブリッジ: `bridgePorts=from:(x,y) via:(x,y)⌒ to:(x,y)` はブリッジを介した接続を表す。via はブリッジ本体の基点で、隣接セルへの直接接続ではない");
            sb.AppendLine("- 段階的な確認: `open` が `.` 以外、または glyph=`*` の場合は、局所的な zoom と隣接する `/active/map/cell_X_Y.md` を先に読み、その後で接続/解体/再建築を指示する");
        }
    }
}
